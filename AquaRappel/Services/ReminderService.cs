namespace AquaRappel.Services;

/// <summary>
/// Rappels eau, pauses écran et coucher.
/// Les rappels sont confiés à Android (AlarmManager) : ils arrivent même application fermée,
/// sans service permanent (éco-conception : aucun processus ne tourne en arrière-plan).
/// </summary>
public class ReminderService
{
    private static readonly string[] WaterMessages =
    {
        "Glou glou ! Un verre d'eau, ça te dit ?",
        "Ton corps a soif, hydrate-toi !",
        "Pause hydratation : une gorgée pour la forme !",
        "Petit rappel : l'eau, c'est la vie !",
    };
    private static readonly Random Rng = new();

    private readonly IDataService _db;
    private readonly IDialogService _ui;
    private readonly INotificationScheduler _scheduler;

    public ReminderService(IDataService db, IDialogService ui, INotificationScheduler scheduler)
    {
        _db = db;
        _ui = ui;
        _scheduler = scheduler;
        _db.DataChanged += () => { if (_db.IsLoggedIn) _scheduler.Reschedule(Build()); };
    }

    private ReminderSchedule Build() => new()
    {
        WaterOn = _db.WaterReminder, WaterEveryMin = _db.WaterEveryMin, WaterDays = _db.DaysOf("water"),
        WaterFrom = _db.WindowOf("water").From, WaterTo = _db.WindowOf("water").To,
        BreakOn = _db.BreakReminder, BreakEveryMin = _db.BreakEveryMin, BreakDays = _db.DaysOf("break"),
        BreakFrom = _db.WindowOf("break").From, BreakTo = _db.WindowOf("break").To,
        BedOn = _db.BedReminder, BedTime = _db.BedTime, BedDays = _db.DaysOf("bed"),
    };

    /// <summary>Appelé après la connexion : demande l'autorisation de notifier puis planifie les rappels.</summary>
    public void Start() => _ = StartAsync();

    private async Task StartAsync()
    {
        try
        {
            await _scheduler.EnsurePermissionAsync();
            _scheduler.Reschedule(Build(), force: true);
        }
        catch { /* les rappels ne doivent jamais empêcher d'utiliser l'application */ }
    }

    public void Stop() => _scheduler.CancelAll();

    public async Task ShowWaterReminderAsync()
    {
        bool drank = await _ui.ConfirmAsync("Pause fraîcheur", WaterMessages[Rng.Next(WaterMessages.Length)], "C'est fait", "Plus tard", "drop");
        if (drank) _db.AddWater(250);
    }

    public async Task ShowBreakReminderAsync()
    {
        bool done = await _ui.ConfirmAsync("Pause écran", "Regarde au loin pendant vingt secondes.", "C'est fait", "Plus tard", "eye");
        if (done) _db.AddBreak();
    }
}
