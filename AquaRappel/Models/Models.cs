namespace AquaRappel.Models;

public class UserAccount
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public class WaterEntry
{
    public string Owner { get; set; } = "";
    public DateTime Time { get; set; }
    public int Ml { get; set; }
}

public class SleepEntry
{
    public string Owner { get; set; } = "";
    public DateTime Date { get; set; }
    public double Hours { get; set; }
    public string Quality { get; set; } = "Bof";
}

public class BreakEntry
{
    public string Owner { get; set; } = "";
    public DateTime Time { get; set; }
}

public class AppSettings
{
    public int GoalMl { get; set; } = 2000;
    public bool WaterReminder { get; set; } = true;
    public int WaterEveryMin { get; set; } = 120;
    public bool BreakReminder { get; set; } = true;
    public int BreakEveryMin { get; set; } = 45;
    // Jours actifs de chaque rappel (lundi -> dimanche)
    public string WaterDays { get; set; } = "1111111";
    public string BreakDays { get; set; } = "1111100";
    public string BedDays { get; set; } = "1111111";
    // Plage horaire de chaque rappel répétitif (on ne dérange pas en dehors)
    public string WaterFrom { get; set; } = "08:00";
    public string WaterTo { get; set; } = "21:00";
    public string BreakFrom { get; set; } = "08:00";
    public string BreakTo { get; set; } = "21:00";
    public bool BedReminder { get; set; } = true;
    public string BedTime { get; set; } = "22:30";
    public long UpdatedAt { get; set; }   // horodatage UTC (ticks) : sert à la synchronisation avec le serveur
}

public class AppData
{
    public List<UserAccount> Users { get; set; } = new();
    public List<WaterEntry> Water { get; set; } = new();
    public List<SleepEntry> Sleep { get; set; } = new();
    public List<BreakEntry> Breaks { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
    public int FailedLogins { get; set; }
    public DateTime? LockedUntil { get; set; }
}

// ---------- Échange avec le serveur (synchronisation) ----------
public class SyncWater { public DateTime Time { get; set; } public int Ml { get; set; } }
public class SyncSleep { public DateTime Date { get; set; } public double Hours { get; set; } public string Quality { get; set; } = "Bof"; }

public class SyncSnapshot
{
    public AppSettings? Settings { get; set; }
    public List<SyncWater> Water { get; set; } = new();
    public List<SyncSleep> Sleep { get; set; } = new();
    public List<DateTime> Breaks { get; set; } = new();
}
