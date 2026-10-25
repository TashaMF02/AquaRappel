using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;

// =====================================================================
//  AquaRappel : serveur (API REST)
//  - comptes (mots de passe hachés PBKDF2) et jetons Bearer aléatoires
//  - synchronisation des données de l'application (eau, sommeil, pauses, réglages)
//  - base de données SQLite (data/aquarappel.db) : tables users, tokens, settings, water, sleep, breaks
// =====================================================================

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_000_000);   // 1 Mo maximum par requête

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);

// Limitation de débit : 120 requêtes par minute et par adresse IP (contre le brute-force et les abus)
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anonyme",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
var store = new Store(Path.Combine(AppContext.BaseDirectory, "data", "aquarappel.db"));

app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

app.MapGet("/", () => "AquaRappel server OK. API : /api/health");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// ---------- Inscription ----------
app.MapPost("/api/auth/register", (RegisterRequest req) =>
{
    string name = (req.Name ?? "").Trim();
    string email = (req.Email ?? "").Trim().ToLowerInvariant();
    string password = req.Password ?? "";

    if (name.Length == 0 || name.Length > 40) return Fail(400, "Le prénom est obligatoire (40 caractères maximum).");
    if (!Validation.Email(email)) return Fail(400, "Adresse email invalide.");
    if (!Validation.Password(password)) return Fail(400, "Mot de passe : 8 caractères minimum, avec au moins une lettre et un chiffre.");

    lock (store.Sync)
    {
        if (store.FindUser(email) != null) return Fail(409, "Un compte existe déjà avec cet email.");
        string hash = Passwords.Hash(password, out string salt);
        store.AddUser(new User { Name = name, Email = email, Salt = salt, Hash = hash });
        string token = store.NewToken(email);
        return Results.Ok(new { token, name });
    }
});

// ---------- Connexion ----------
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    string email = (req.Email ?? "").Trim().ToLowerInvariant();
    string password = req.Password ?? "";

    lock (store.Sync)
    {
        User? user = store.FindUser(email);

        if (user?.LockedUntil is DateTime until && until > DateTime.UtcNow)
            return Fail(429, "Trop de tentatives. Réessaie dans quelques minutes.");

        bool ok;
        if (user == null) { Passwords.Hash(password, out _); ok = false; }   // même durée : on ne révèle pas si le compte existe
        else ok = Passwords.Verify(password, user.Salt, user.Hash);

        if (!ok)
        {
            if (user != null)
            {
                user.Failed++;
                if (user.Failed >= 5) { user.LockedUntil = DateTime.UtcNow.AddMinutes(5); user.Failed = 0; }
                store.UpdateLock(user);
            }
            return Fail(401, "Email ou mot de passe incorrect.");
        }

        user!.Failed = 0;
        user.LockedUntil = null;
        store.UpdateLock(user);
        string token = store.NewToken(email);
        return Results.Ok(new { token, name = user.Name });
    }
});

// ---------- Déconnexion ----------
app.MapPost("/api/auth/logout", (HttpContext ctx) =>
{
    lock (store.Sync)
    {
        string? hash = store.TokenHash(ctx);
        if (hash != null) store.RemoveToken(hash);
    }
    return Results.Ok(new { ok = true });
});

// ---------- Synchronisation : fusionne les données reçues avec celles du serveur et renvoie le tout ----------
app.MapPost("/api/sync", (HttpContext ctx, Snapshot incoming) =>
{
    lock (store.Sync)
    {
        string? email = store.Authenticate(ctx);
        if (email == null) return Fail(401, "Session invalide.");

        Snapshot merged = Merge.Run(store.LoadSnapshot(email), incoming);
        store.SaveSnapshot(email, merged);
        return Results.Ok(merged);
    }
});

app.MapGet("/api/sync", (HttpContext ctx) =>
{
    lock (store.Sync)
    {
        string? email = store.Authenticate(ctx);
        if (email == null) return Fail(401, "Session invalide.");
        return Results.Ok(store.LoadSnapshot(email));
    }
});

// ---------- Suppression du compte et de toutes ses données (droit à l'effacement) ----------
app.MapDelete("/api/account", (HttpContext ctx) =>
{
    lock (store.Sync)
    {
        string? email = store.Authenticate(ctx);
        if (email == null) return Fail(401, "Session invalide.");
        store.DeleteAccount(email);   // les tables liées sont vidées en cascade
        return Results.Ok(new { deleted = true });
    }
});

app.Run();

static IResult Fail(int status, string message) => Results.Json(new { error = message }, statusCode: status);

// =====================================================================
//  Modèles
// =====================================================================
record RegisterRequest(string? Name, string? Email, string? Password);
record LoginRequest(string? Email, string? Password);

class User
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Failed { get; set; }
    public DateTime? LockedUntil { get; set; }
}

class Settings
{
    public int GoalMl { get; set; } = 2000;
    public bool WaterReminder { get; set; } = true;
    public int WaterEveryMin { get; set; } = 120;
    public bool BreakReminder { get; set; } = true;
    public int BreakEveryMin { get; set; } = 45;
    public string WaterDays { get; set; } = "1111111";
    public string BreakDays { get; set; } = "1111100";
    public string BedDays { get; set; } = "1111111";
    public string WaterFrom { get; set; } = "08:00";
    public string WaterTo { get; set; } = "21:00";
    public string BreakFrom { get; set; } = "08:00";
    public string BreakTo { get; set; } = "21:00";
    public bool BedReminder { get; set; } = true;
    public string BedTime { get; set; } = "22:30";
    public long UpdatedAt { get; set; }
}

class WaterItem { public DateTime Time { get; set; } public int Ml { get; set; } }
class SleepItem { public DateTime Date { get; set; } public double Hours { get; set; } public string Quality { get; set; } = "Bof"; }

class Snapshot
{
    public Settings? Settings { get; set; }
    public List<WaterItem> Water { get; set; } = new();
    public List<SleepItem> Sleep { get; set; } = new();
    public List<DateTime> Breaks { get; set; } = new();
}

// =====================================================================
//  Base de données SQLite (ADO.NET, requêtes paramétrées : pas d'injection SQL)
// =====================================================================
class Store
{
    private const string F = "yyyy-MM-ddTHH:mm:ss.fffffff";   // format de date texte triable
    private readonly string _cs;
    public readonly object Sync = new();

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS users (
            email        TEXT PRIMARY KEY,
            name         TEXT NOT NULL,
            salt         TEXT NOT NULL,
            hash         TEXT NOT NULL,
            failed       INTEGER NOT NULL DEFAULT 0,
            locked_until TEXT
        );
        CREATE TABLE IF NOT EXISTS tokens (
            hash    TEXT PRIMARY KEY,
            email   TEXT NOT NULL REFERENCES users(email) ON DELETE CASCADE,
            expires TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS settings (
            email          TEXT PRIMARY KEY REFERENCES users(email) ON DELETE CASCADE,
            goal_ml        INTEGER NOT NULL,
            water_reminder INTEGER NOT NULL,
            water_every    INTEGER NOT NULL,
            water_from     TEXT NOT NULL,
            water_to       TEXT NOT NULL,
            water_days     TEXT NOT NULL,
            break_reminder INTEGER NOT NULL,
            break_every    INTEGER NOT NULL,
            break_from     TEXT NOT NULL,
            break_to       TEXT NOT NULL,
            break_days     TEXT NOT NULL,
            bed_reminder   INTEGER NOT NULL,
            bed_time       TEXT NOT NULL,
            bed_days       TEXT NOT NULL,
            updated_at     INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS water (
            email TEXT NOT NULL REFERENCES users(email) ON DELETE CASCADE,
            time  TEXT NOT NULL,
            ml    INTEGER NOT NULL CHECK (ml BETWEEN 1 AND 5000),
            PRIMARY KEY (email, time, ml)
        );
        CREATE TABLE IF NOT EXISTS sleep (
            email   TEXT NOT NULL REFERENCES users(email) ON DELETE CASCADE,
            date    TEXT NOT NULL,
            hours   REAL NOT NULL CHECK (hours BETWEEN 0 AND 24),
            quality TEXT NOT NULL CHECK (quality IN ('Mal', 'Bof', 'Top')),
            PRIMARY KEY (email, date)
        );
        CREATE TABLE IF NOT EXISTS breaks (
            email TEXT NOT NULL REFERENCES users(email) ON DELETE CASCADE,
            time  TEXT NOT NULL,
            PRIMARY KEY (email, time)
        );
        CREATE INDEX IF NOT EXISTS idx_water_email_time ON water(email, time);
        """;

    public Store(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _cs = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true }.ToString();
        using var c = Open();
        Run(c, "PRAGMA journal_mode=WAL;");
        Run(c, Schema);
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        return c;
    }

    private static SqliteCommand Cmd(SqliteConnection c, string sql, params (string Name, object? Value)[] p)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in p) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static int Run(SqliteConnection c, string sql, params (string, object?)[] p)
    {
        using var cmd = Cmd(c, sql, p);
        return cmd.ExecuteNonQuery();
    }

    private static string D(DateTime d) => d.ToString(F, CultureInfo.InvariantCulture);
    private static DateTime P(string s) => DateTime.ParseExact(s, F, CultureInfo.InvariantCulture, DateTimeStyles.None);

    // ---------- Comptes ----------
    public User? FindUser(string email)
    {
        using var c = Open();
        using var cmd = Cmd(c, "SELECT name, salt, hash, failed, locked_until FROM users WHERE email = $e", ("$e", email));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new User
        {
            Email = email, Name = r.GetString(0), Salt = r.GetString(1), Hash = r.GetString(2), Failed = r.GetInt32(3),
            LockedUntil = r.IsDBNull(4) ? null : P(r.GetString(4)),
        };
    }

    public void AddUser(User u)
    {
        using var c = Open();
        Run(c, "INSERT INTO users (email, name, salt, hash) VALUES ($e, $n, $s, $h)",
            ("$e", u.Email), ("$n", u.Name), ("$s", u.Salt), ("$h", u.Hash));
    }

    public void UpdateLock(User u)
    {
        using var c = Open();
        Run(c, "UPDATE users SET failed = $f, locked_until = $l WHERE email = $e",
            ("$f", u.Failed), ("$l", u.LockedUntil == null ? null : D(u.LockedUntil.Value)), ("$e", u.Email));
    }

    public void DeleteAccount(string email)
    {
        using var c = Open();
        Run(c, "DELETE FROM users WHERE email = $e", ("$e", email));
    }

    // ---------- Jetons : seul le SHA-256 est conservé ----------
    private static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>Jeton aléatoire de 256 bits, valable 30 jours, 5 appareils maximum par compte.</summary>
    public string NewToken(string email)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        using var c = Open();
        Run(c, "DELETE FROM tokens WHERE expires < $now", ("$now", D(DateTime.UtcNow)));
        Run(c, "INSERT INTO tokens (hash, email, expires) VALUES ($h, $e, $x)",
            ("$h", Sha(token)), ("$e", email), ("$x", D(DateTime.UtcNow.AddDays(30))));
        Run(c, "DELETE FROM tokens WHERE email = $e AND hash NOT IN (SELECT hash FROM tokens WHERE email = $e ORDER BY expires DESC LIMIT 5)",
            ("$e", email));
        return token;
    }

    public void RemoveToken(string hash)
    {
        using var c = Open();
        Run(c, "DELETE FROM tokens WHERE hash = $h", ("$h", hash));
    }

    public string? TokenHash(HttpContext ctx)
    {
        string header = ctx.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) && header.Length > 7 ? Sha(header[7..].Trim()) : null;
    }

    public string? Authenticate(HttpContext ctx)
    {
        string? hash = TokenHash(ctx);
        if (hash == null) return null;
        using var c = Open();
        using var cmd = Cmd(c, "SELECT email FROM tokens WHERE hash = $h AND expires > $now", ("$h", hash), ("$now", D(DateTime.UtcNow)));
        return cmd.ExecuteScalar() as string;
    }

    // ---------- Données de l'utilisateur ----------
    public Snapshot LoadSnapshot(string email)
    {
        var s = new Snapshot();
        using var c = Open();

        using (var cmd = Cmd(c, @"SELECT goal_ml, water_reminder, water_every, water_from, water_to, water_days,
                                         break_reminder, break_every, break_from, break_to, break_days,
                                         bed_reminder, bed_time, bed_days, updated_at
                                  FROM settings WHERE email = $e", ("$e", email)))
        using (var r = cmd.ExecuteReader())
            if (r.Read())
                s.Settings = new Settings
                {
                    GoalMl = r.GetInt32(0), WaterReminder = r.GetInt32(1) != 0, WaterEveryMin = r.GetInt32(2),
                    WaterFrom = r.GetString(3), WaterTo = r.GetString(4), WaterDays = r.GetString(5),
                    BreakReminder = r.GetInt32(6) != 0, BreakEveryMin = r.GetInt32(7),
                    BreakFrom = r.GetString(8), BreakTo = r.GetString(9), BreakDays = r.GetString(10),
                    BedReminder = r.GetInt32(11) != 0, BedTime = r.GetString(12), BedDays = r.GetString(13),
                    UpdatedAt = r.GetInt64(14),
                };

        using (var cmd = Cmd(c, "SELECT time, ml FROM water WHERE email = $e ORDER BY time", ("$e", email)))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) s.Water.Add(new WaterItem { Time = P(r.GetString(0)), Ml = r.GetInt32(1) });

        using (var cmd = Cmd(c, "SELECT date, hours, quality FROM sleep WHERE email = $e ORDER BY date", ("$e", email)))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) s.Sleep.Add(new SleepItem { Date = P(r.GetString(0)), Hours = r.GetDouble(1), Quality = r.GetString(2) });

        using (var cmd = Cmd(c, "SELECT time FROM breaks WHERE email = $e ORDER BY time", ("$e", email)))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) s.Breaks.Add(P(r.GetString(0)));

        return s;
    }

    /// <summary>Remplace, dans une transaction, les données de l'utilisateur par la version fusionnée.</summary>
    public void SaveSnapshot(string email, Snapshot s)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();

        Run(c, "DELETE FROM water WHERE email = $e", ("$e", email));
        Run(c, "DELETE FROM sleep WHERE email = $e", ("$e", email));
        Run(c, "DELETE FROM breaks WHERE email = $e", ("$e", email));

        foreach (var w in s.Water)
            Run(c, "INSERT OR IGNORE INTO water (email, time, ml) VALUES ($e, $t, $m)", ("$e", email), ("$t", D(w.Time)), ("$m", w.Ml));
        foreach (var n in s.Sleep)
            Run(c, "INSERT OR REPLACE INTO sleep (email, date, hours, quality) VALUES ($e, $d, $h, $q)",
                ("$e", email), ("$d", D(n.Date)), ("$h", n.Hours), ("$q", n.Quality));
        foreach (var b in s.Breaks)
            Run(c, "INSERT OR IGNORE INTO breaks (email, time) VALUES ($e, $t)", ("$e", email), ("$t", D(b)));

        if (s.Settings is Settings x)
            Run(c, @"INSERT OR REPLACE INTO settings
                        (email, goal_ml, water_reminder, water_every, water_from, water_to, water_days,
                         break_reminder, break_every, break_from, break_to, break_days,
                         bed_reminder, bed_time, bed_days, updated_at)
                     VALUES ($e, $g, $wr, $we, $wf, $wt, $wd, $br, $be, $bf, $bt, $bd, $ber, $bti, $bda, $u)",
                ("$e", email), ("$g", x.GoalMl), ("$wr", x.WaterReminder ? 1 : 0), ("$we", x.WaterEveryMin),
                ("$wf", x.WaterFrom), ("$wt", x.WaterTo), ("$wd", x.WaterDays),
                ("$br", x.BreakReminder ? 1 : 0), ("$be", x.BreakEveryMin), ("$bf", x.BreakFrom), ("$bt", x.BreakTo), ("$bd", x.BreakDays),
                ("$ber", x.BedReminder ? 1 : 0), ("$bti", x.BedTime), ("$bda", x.BedDays), ("$u", x.UpdatedAt));

        tx.Commit();
    }
}

// =====================================================================
//  Mots de passe et validation
// =====================================================================
static class Passwords
{
    public static string Hash(string password, out string salt)
    {
        byte[] s = RandomNumberGenerator.GetBytes(16);
        salt = Convert.ToBase64String(s);
        return Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, s, 100_000, HashAlgorithmName.SHA256, 32));
    }

    public static bool Verify(string password, string salt, string hash)
    {
        byte[] test = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(test, Convert.FromBase64String(hash));
    }
}

static class Validation
{
    public static bool Email(string e)
    {
        try { return e.Length <= 100 && new MailAddress(e).Address == e; }
        catch { return false; }
    }

    public static bool Password(string p) => p.Length is >= 8 and <= 64 && p.Any(char.IsLetter) && p.Any(char.IsDigit);

    public static bool Days(string? d) => d != null && Regex.IsMatch(d, "^[01]{7}$");
    public static bool Time(string? t) => t != null && TimeSpan.TryParseExact(t, @"hh\:mm", null, out _);
}

// =====================================================================
//  Fusion : on ajoute ce qui manque, on ne supprime rien ; réglages = le plus récent gagne
// =====================================================================
static class Merge
{
    private const int KeepDays = 120;

    public static Snapshot Run(Snapshot current, Snapshot incoming)
    {
        DateTime from = DateTime.Today.AddDays(-KeepDays);
        var result = new Snapshot { Settings = current.Settings };

        // Réglages : le plus récent (UpdatedAt) l'emporte, après nettoyage des valeurs
        if (incoming.Settings != null && (current.Settings == null || incoming.Settings.UpdatedAt > current.Settings.UpdatedAt))
            result.Settings = Clean(incoming.Settings);

        var water = new HashSet<(DateTime, int)>();
        foreach (var w in current.Water.Concat(incoming.Water.Take(20000)))
            if (w.Ml is > 0 and <= 5000 && w.Time >= from && water.Add((w.Time, w.Ml)))
                result.Water.Add(new WaterItem { Time = w.Time, Ml = w.Ml });

        var nights = new Dictionary<DateTime, SleepItem>();
        foreach (var s in current.Sleep.Concat(incoming.Sleep.Take(2000)))
            if (s.Hours is >= 0 and <= 24 && s.Date >= from)
                nights[s.Date.Date] = new SleepItem
                {
                    Date = s.Date.Date, Hours = s.Hours,
                    Quality = s.Quality is "Mal" or "Bof" or "Top" ? s.Quality : "Bof",
                };
        result.Sleep = nights.Values.OrderBy(s => s.Date).ToList();

        result.Breaks = current.Breaks.Concat(incoming.Breaks.Take(20000))
            .Where(b => b >= from).Distinct().OrderBy(b => b).ToList();

        result.Water = result.Water.OrderBy(w => w.Time).ToList();
        return result;
    }

    private static Settings Clean(Settings s) => new()
    {
        GoalMl = Math.Clamp(s.GoalMl, 500, 5000),
        WaterReminder = s.WaterReminder,
        WaterEveryMin = Math.Clamp(s.WaterEveryMin, 1, 1440),
        BreakReminder = s.BreakReminder,
        BreakEveryMin = Math.Clamp(s.BreakEveryMin, 1, 1440),
        WaterFrom = Validation.Time(s.WaterFrom) ? s.WaterFrom : "08:00",
        WaterTo = Validation.Time(s.WaterTo) ? s.WaterTo : "21:00",
        BreakFrom = Validation.Time(s.BreakFrom) ? s.BreakFrom : "08:00",
        BreakTo = Validation.Time(s.BreakTo) ? s.BreakTo : "21:00",
        WaterDays = Validation.Days(s.WaterDays) ? s.WaterDays : "1111111",
        BreakDays = Validation.Days(s.BreakDays) ? s.BreakDays : "1111100",
        BedDays = Validation.Days(s.BedDays) ? s.BedDays : "1111111",
        BedReminder = s.BedReminder,
        BedTime = Validation.Time(s.BedTime) ? s.BedTime : "22:30",
        UpdatedAt = s.UpdatedAt,
    };
}
