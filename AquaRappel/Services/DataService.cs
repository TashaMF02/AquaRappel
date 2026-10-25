using System.Net.Mail;
using System.Text.Json;
using AquaRappel.Models;

namespace AquaRappel.Services;

public interface IDataService
{
    event Action? DataChanged;

    bool IsLoggedIn { get; }
    bool HasAccount(string email);
    SyncSnapshot ExportSnapshot();
    void ImportSnapshot(SyncSnapshot snapshot);
    string CurrentName { get; }
    string? Register(string name, string email, string password);
    string? Login(string email, string password);
    void Logout();

    int GoalMl { get; }
    void SetGoal(int ml);
    bool WaterReminder { get; }
    void SetWaterReminder(bool on);
    int WaterEveryMin { get; }
    void SetWaterEvery(int min);
    bool BreakReminder { get; }
    void SetBreakReminder(bool on);
    int BreakEveryMin { get; }
    void SetBreakEvery(int min);
    bool BedReminder { get; }
    void SetBedReminder(bool on);
    string BedTime { get; }
    void SetBedTime(string hhmm);
    /// <summary>kind : "water", "break" ou "bed"</summary>
    string DaysOf(string kind);
    (string From, string To) WindowOf(string kind);
    void SetWindow(string kind, string from, string to);
    void SetDays(string kind, string days);
    bool IsDayActive(DateTime day, string kind);

    void AddWater(int ml);
    int TodayWater { get; }
    List<(DateTime Day, int Ml)> LastDays(int n);

    void SaveSleep(double hours, string quality);
    SleepEntry? LastSleep { get; }
    double? AvgSleepLast7 { get; }

    void AddBreak();
    int BreaksOn(DateTime day);
    int BreaksLast7 { get; }
}

/// <summary>Back-end local : données chiffrées (AES-GCM) dans le dossier privé de l'application.</summary>
public sealed class DataService : IDataService
{
    private const int MaxFailures = 5;
    private const int LockMinutes = 5;

    private readonly ISecurityService _sec;
    private readonly string _path;
    private AppData _d = new AppData();
    private string? _email;

    public event Action? DataChanged;

    public DataService(ISecurityService sec)
    {
        _sec = sec;
        _path = Path.Combine(FileSystem.AppDataDirectory, "aquarappel.dat");
        Load();

        string? session = _sec.GetSession();
        _email = _d.Users.Any(u => u.Email == session) ? session : null;
    }

    // ---------- Persistance ----------
    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                byte[] plain = _sec.Decrypt(File.ReadAllBytes(_path));
                _d = JsonSerializer.Deserialize<AppData>(plain) ?? new AppData();
            }
        }
        catch
        {
            _d = new AppData();   // fichier illisible, modifié ou ancien format : on repart de zéro
        }
    }

    private void Save()
    {
        string tmp = _path + ".tmp";
        File.WriteAllBytes(tmp, _sec.Encrypt(JsonSerializer.SerializeToUtf8Bytes(_d)));
        File.Move(tmp, _path, true);
    }

    private void Changed()
    {
        _d.Settings.UpdatedAt = DateTime.UtcNow.Ticks;
        Save();
        DataChanged?.Invoke();
    }

    private string Owner => _email ?? "";

    // ---------- Comptes ----------
    private UserAccount? CurrentUser => _d.Users.FirstOrDefault(u => u.Email == _email);
    public bool IsLoggedIn => CurrentUser != null;
    public string CurrentName => CurrentUser?.Name ?? "toi";

    private static bool ValidEmail(string e)
    {
        try { return e.Length <= 100 && new MailAddress(e).Address == e; }
        catch { return false; }
    }

    /// <summary>Retourne null si tout va bien, sinon le message d'erreur.</summary>
    public string? Register(string name, string email, string password)
    {
        name = (name ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        password ??= "";

        if (name.Length == 0 || name.Length > 40) return "Le prénom est obligatoire (40 caractères maximum).";
        if (!ValidEmail(email)) return "Adresse email invalide.";
        if (password.Length < 8 || password.Length > 64 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Mot de passe : 8 caractères minimum, avec au moins une lettre et un chiffre.";
        if (_d.Users.Any(u => u.Email == email)) return "Un compte existe déjà avec cet email.";

        string hash = _sec.HashPassword(password, out string salt);
        _d.Users.Add(new UserAccount { Name = name, Email = email, Salt = salt, PasswordHash = hash });
        _email = email;
        _sec.SetSession(email);
        Changed();
        return null;
    }

    public string? Login(string email, string password)
    {
        if (_d.LockedUntil is DateTime until && until > DateTime.Now)
            return $"Trop de tentatives. Réessayez dans {Math.Ceiling((until - DateTime.Now).TotalMinutes)} min.";

        email = (email ?? "").Trim().ToLowerInvariant();
        password ??= "";
        var user = _d.Users.FirstOrDefault(u => u.Email == email);

        bool ok;
        if (user == null)
        {
            _sec.HashPassword(password, out _);   // même durée de calcul : on ne révèle pas si le compte existe
            ok = false;
        }
        else ok = _sec.Verify(password, user.Salt, user.PasswordHash);

        if (!ok)
        {
            _d.FailedLogins++;
            if (_d.FailedLogins >= MaxFailures)
            {
                _d.LockedUntil = DateTime.Now.AddMinutes(LockMinutes);
                _d.FailedLogins = 0;
            }
            Save();
            return "Email ou mot de passe incorrect.";
        }

        _d.FailedLogins = 0;
        _d.LockedUntil = null;
        _email = email;
        _sec.SetSession(email);
        Changed();
        return null;
    }

    public void Logout()
    {
        _email = null;
        _sec.SetSession(null);
        DataChanged?.Invoke();
    }

    // ---------- Synchronisation ----------
    private const int KeepDays = 120;   // on n'échange que les 4 derniers mois (peu de données = éco-conception)

    public bool HasAccount(string email)
        => _d.Users.Any(u => u.Email == (email ?? "").Trim().ToLowerInvariant());

    public SyncSnapshot ExportSnapshot()
    {
        DateTime from = DateTime.Today.AddDays(-KeepDays);
        return new SyncSnapshot
        {
            Settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_d.Settings)),
            Water = _d.Water.Where(w => w.Owner == Owner && w.Time >= from)
                            .Select(w => new SyncWater { Time = w.Time, Ml = w.Ml }).ToList(),
            Sleep = _d.Sleep.Where(s => s.Owner == Owner && s.Date >= from)
                            .Select(s => new SyncSleep { Date = s.Date, Hours = s.Hours, Quality = s.Quality }).ToList(),
            Breaks = _d.Breaks.Where(b => b.Owner == Owner && b.Time >= from).Select(b => b.Time).ToList(),
        };
    }

    /// <summary>Fusionne les données reçues du serveur (sans jamais rien supprimer en local).</summary>
    public void ImportSnapshot(SyncSnapshot s)
    {
        if (!IsLoggedIn || s == null) return;
        DateTime from = DateTime.Today.AddDays(-KeepDays);
        bool changed = false;

        var water = new HashSet<(DateTime, int)>(_d.Water.Where(w => w.Owner == Owner).Select(w => (w.Time, w.Ml)));
        foreach (var w in s.Water.Take(20000))
            if (w.Ml is > 0 and <= 5000 && w.Time >= from && water.Add((w.Time, w.Ml)))
            {
                _d.Water.Add(new WaterEntry { Owner = Owner, Time = w.Time, Ml = w.Ml });
                changed = true;
            }

        var nights = new HashSet<DateTime>(_d.Sleep.Where(x => x.Owner == Owner).Select(x => x.Date.Date));
        foreach (var n in s.Sleep.Take(2000))
            if (n.Hours is >= 0 and <= 24 && n.Date >= from && nights.Add(n.Date.Date))
            {
                string q = n.Quality is "Mal" or "Bof" or "Top" ? n.Quality : "Bof";
                _d.Sleep.Add(new SleepEntry { Owner = Owner, Date = n.Date.Date, Hours = n.Hours, Quality = q });
                changed = true;
            }

        var breaks = new HashSet<DateTime>(_d.Breaks.Where(b => b.Owner == Owner).Select(b => b.Time));
        foreach (var b in s.Breaks.Take(20000))
            if (b >= from && breaks.Add(b))
            {
                _d.Breaks.Add(new BreakEntry { Owner = Owner, Time = b });
                changed = true;
            }

        if (s.Settings != null && s.Settings.UpdatedAt > _d.Settings.UpdatedAt)
        {
            _d.Settings = Sanitize(s.Settings);
            changed = true;
        }

        if (changed)
        {
            Save();
            DataChanged?.Invoke();   // pas de Changed() : on ne modifie pas l'horodatage des réglages
        }
    }

    private static string Hm(string? t, string fallback)
        => TimeSpan.TryParseExact(t, @"hh\:mm", null, out _) ? t! : fallback;

    private static AppSettings Sanitize(AppSettings a)
    {
        static string Days(string d, string fallback) => d != null && d.Length == 7 && d.All(c => c == '0' || c == '1') ? d : fallback;
        return new AppSettings
        {
            GoalMl = Math.Clamp(a.GoalMl, 500, 5000),
            WaterReminder = a.WaterReminder,
            WaterEveryMin = Math.Clamp(a.WaterEveryMin, 1, 1440),
            BreakReminder = a.BreakReminder,
            BreakEveryMin = Math.Clamp(a.BreakEveryMin, 1, 1440),
            WaterFrom = Hm(a.WaterFrom, "08:00"), WaterTo = Hm(a.WaterTo, "21:00"),
            BreakFrom = Hm(a.BreakFrom, "08:00"), BreakTo = Hm(a.BreakTo, "21:00"),
            WaterDays = Days(a.WaterDays, "1111111"),
            BreakDays = Days(a.BreakDays, "1111100"),
            BedDays = Days(a.BedDays, "1111111"),
            BedReminder = a.BedReminder,
            BedTime = TimeSpan.TryParseExact(a.BedTime, @"hh\:mm", null, out _) ? a.BedTime : "22:30",
            UpdatedAt = a.UpdatedAt,
        };
    }

    // ---------- Réglages ----------
    public int GoalMl => _d.Settings.GoalMl;
    public void SetGoal(int ml) { _d.Settings.GoalMl = Math.Clamp(ml, 500, 5000); Changed(); }

    public bool WaterReminder => _d.Settings.WaterReminder;
    public void SetWaterReminder(bool on) { _d.Settings.WaterReminder = on; Changed(); }
    public int WaterEveryMin => _d.Settings.WaterEveryMin;
    public void SetWaterEvery(int min) { _d.Settings.WaterEveryMin = Math.Clamp(min, 1, 1440); Changed(); }

    public bool BreakReminder => _d.Settings.BreakReminder;
    public void SetBreakReminder(bool on) { _d.Settings.BreakReminder = on; Changed(); }
    public int BreakEveryMin => _d.Settings.BreakEveryMin;
    public void SetBreakEvery(int min) { _d.Settings.BreakEveryMin = Math.Clamp(min, 1, 1440); Changed(); }

    public bool BedReminder => _d.Settings.BedReminder;
    public void SetBedReminder(bool on) { _d.Settings.BedReminder = on; Changed(); }
    public string BedTime => _d.Settings.BedTime;
    public void SetBedTime(string hhmm)
    {
        if (TimeSpan.TryParseExact(hhmm, @"hh\:mm", null, out _)) { _d.Settings.BedTime = hhmm; Changed(); }
    }

    public string DaysOf(string kind) => kind switch
    {
        "water" => _d.Settings.WaterDays,
        "break" => _d.Settings.BreakDays,
        _ => _d.Settings.BedDays,
    };

    public void SetDays(string kind, string days)
    {
        if (days.Length != 7 || !days.All(c => c == '0' || c == '1')) return;
        switch (kind)
        {
            case "water": _d.Settings.WaterDays = days; break;
            case "break": _d.Settings.BreakDays = days; break;
            default: _d.Settings.BedDays = days; break;
        }
        Changed();
    }

    public (string From, string To) WindowOf(string kind)
        => kind == "water" ? (_d.Settings.WaterFrom, _d.Settings.WaterTo) : (_d.Settings.BreakFrom, _d.Settings.BreakTo);

    public void SetWindow(string kind, string from, string to)
    {
        if (!TimeSpan.TryParseExact(from, @"hh\:mm", null, out _) || !TimeSpan.TryParseExact(to, @"hh\:mm", null, out _)) return;
        if (kind == "water") { _d.Settings.WaterFrom = from; _d.Settings.WaterTo = to; }
        else { _d.Settings.BreakFrom = from; _d.Settings.BreakTo = to; }
        Changed();
    }

    public bool IsDayActive(DateTime day, string kind)
    {
        int i = ((int)day.DayOfWeek + 6) % 7;   // lundi = 0
        string d = DaysOf(kind);
        return i < d.Length && d[i] == '1';
    }

    // ---------- Eau ----------
    public void AddWater(int ml)
    {
        if (ml <= 0 || ml > 5000) return;
        _d.Water.Add(new WaterEntry { Owner = Owner, Time = DateTime.Now, Ml = ml });
        Changed();
    }

    private int WaterOn(DateTime day)
        => _d.Water.Where(w => w.Owner == Owner && w.Time.Date == day.Date).Sum(w => w.Ml);

    public int TodayWater => WaterOn(DateTime.Today);

    public List<(DateTime Day, int Ml)> LastDays(int n)
    {
        var list = new List<(DateTime Day, int Ml)>();
        for (int i = n - 1; i >= 0; i--)
        {
            DateTime d = DateTime.Today.AddDays(-i);
            list.Add((d, WaterOn(d)));
        }
        return list;
    }

    // ---------- Sommeil ----------
    public void SaveSleep(double hours, string quality)
    {
        _d.Sleep.RemoveAll(s => s.Owner == Owner && s.Date == DateTime.Today);
        _d.Sleep.Add(new SleepEntry { Owner = Owner, Date = DateTime.Today, Hours = Math.Clamp(hours, 0, 24), Quality = quality });
        Changed();
    }

    public SleepEntry? LastSleep => _d.Sleep.Where(s => s.Owner == Owner).OrderByDescending(s => s.Date).FirstOrDefault();

    public double? AvgSleepLast7
    {
        get
        {
            DateTime from = DateTime.Today.AddDays(-6);
            var l = _d.Sleep.Where(s => s.Owner == Owner && s.Date >= from).ToList();
            return l.Count == 0 ? (double?)null : l.Average(s => s.Hours);
        }
    }

    // ---------- Pauses écran ----------
    public void AddBreak()
    {
        _d.Breaks.Add(new BreakEntry { Owner = Owner, Time = DateTime.Now });
        Changed();
    }

    public int BreaksOn(DateTime day) => _d.Breaks.Count(b => b.Owner == Owner && b.Time.Date == day.Date);
    public int BreaksLast7 => _d.Breaks.Count(b => b.Owner == Owner && b.Time.Date >= DateTime.Today.AddDays(-6));
}
