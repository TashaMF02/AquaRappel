namespace AquaRappel.Services;

/// <summary>Réglages des rappels, copiés dans un petit fichier lisible par Android même application fermée.</summary>
public class ReminderSchedule
{
    public bool WaterOn { get; set; }
    public int WaterEveryMin { get; set; } = 120;
    public string WaterDays { get; set; } = "1111111";
    public string WaterFrom { get; set; } = "08:00";
    public string WaterTo { get; set; } = "21:00";
    public string BreakFrom { get; set; } = "08:00";
    public string BreakTo { get; set; } = "21:00";
    public bool BreakOn { get; set; }
    public int BreakEveryMin { get; set; } = 45;
    public string BreakDays { get; set; } = "1111100";
    public bool BedOn { get; set; }
    public string BedTime { get; set; } = "22:30";
    public string BedDays { get; set; } = "1111111";
}

/// <summary>Calcule la prochaine heure de chaque rappel (logique pure, indépendante d'Android).</summary>
public static class ReminderPlan
{
    public static readonly string[] Kinds = { "water", "break", "bed" };

    /// <summary>Plage horaire choisie par l'utilisateur ; si elle est incohérente on prend toute la journée.</summary>
    private static (TimeSpan Start, TimeSpan End) Window(string from, string to)
    {
        if (TimeSpan.TryParse(from, out TimeSpan a) && TimeSpan.TryParse(to, out TimeSpan b) && b > a) return (a, b);
        return (TimeSpan.Zero, new TimeSpan(23, 59, 59));
    }

    public static bool IsOn(string kind, ReminderSchedule s) => kind switch
    {
        "water" => s.WaterOn,
        "break" => s.BreakOn,
        "bed" => s.BedOn,
        _ => false,
    };

    private static bool DayOn(string days, DateTime d)
    {
        int i = ((int)d.DayOfWeek + 6) % 7;   // lundi = 0
        return i < days.Length && days[i] == '1';
    }

    public static DateTime? Next(string kind, ReminderSchedule s, DateTime from)
    {
        if (!IsOn(kind, s)) return null;

        if (kind == "bed")
        {
            if (!TimeSpan.TryParse(s.BedTime, out TimeSpan t)) return null;
            for (int i = 0; i <= 7; i++)
            {
                DateTime c = from.Date.AddDays(i) + t;
                if (c > from && DayOn(s.BedDays, c)) return c;
            }
            return null;
        }

        string days = kind == "water" ? s.WaterDays : s.BreakDays;
        int every = Math.Max(1, kind == "water" ? s.WaterEveryMin : s.BreakEveryMin);
        var (start, end) = kind == "water" ? Window(s.WaterFrom, s.WaterTo) : Window(s.BreakFrom, s.BreakTo);
        DateTime next = from.AddMinutes(every);
        for (int i = 0; i < 30; i++)
        {
            if (DayOn(days, next))
            {
                TimeSpan tod = next.TimeOfDay;
                if (tod < start) next = next.Date + start;
                else if (tod <= end) return next;
                else next = next.Date.AddDays(1) + start;
            }
            else next = next.Date.AddDays(1) + start;
        }
        return null;
    }
}

/// <summary>Planification des notifications (implémentée par Android avec AlarmManager).</summary>
public interface INotificationScheduler
{
    Task<bool> EnsurePermissionAsync();
    /// <summary>force = true : on replanifie même si les réglages n'ont pas changé.</summary>
    void Reschedule(ReminderSchedule schedule, bool force = false);
    void CancelAll();
}

public class NoopNotificationScheduler : INotificationScheduler
{
    public Task<bool> EnsurePermissionAsync() => Task.FromResult(false);
    public void Reschedule(ReminderSchedule schedule, bool force = false) { }
    public void CancelAll() { }
}
