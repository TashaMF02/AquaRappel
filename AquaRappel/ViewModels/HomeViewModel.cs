using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private const int GlassMl = 250;

    private readonly IDataService _db;
    private readonly IWeatherService _weather;
    private readonly IDialogService _ui;
    private readonly INavigationService _nav;
    private readonly ReminderService _reminders;
    private readonly IThemeService _theme;
    private readonly ISyncService _sync;
    private readonly IApiService _api;

    private string _greeting = "";
    private string _amountText = "";
    private string _goalText = "";
    private string _glassesText = "";
    private string _messageText = "";
    private string _sleepText = "—";
    private string _breaksText = "0 / 6";
    private string _nextReminderText = "";
    private string _advice = "Conseil météo : chargement…";

    private DateTime _weatherAt = DateTime.MinValue;
    private bool _weatherBusy;

    public HomeViewModel(IDataService db, IWeatherService weather, IDialogService ui,
                         INavigationService nav, ReminderService reminders, IThemeService theme,
                         ISyncService sync, IApiService api)
    {
        _db = db;
        _weather = weather;
        _ui = ui;
        _nav = nav;
        _reminders = reminders;
        _theme = theme;
        _sync = sync;
        _api = api;

        AddWaterCommand = new RelayCommand<string>(AddWater);
        QuickEntryCommand = new AsyncRelayCommand(QuickEntryAsync);
        OpenGoalCommand = new AsyncRelayCommand(() => _nav.GoToAsync("goal"));
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);

        _db.DataChanged += Refresh;   // l'écran se met à jour dès qu'une donnée change
    }

    public string Greeting { get => _greeting; set => SetProperty(ref _greeting, value); }
    public string AmountText { get => _amountText; set => SetProperty(ref _amountText, value); }
    public string GoalText { get => _goalText; set => SetProperty(ref _goalText, value); }
    public string GlassesText { get => _glassesText; set => SetProperty(ref _glassesText, value); }
    public string MessageText { get => _messageText; set => SetProperty(ref _messageText, value); }
    public string SleepText { get => _sleepText; set => SetProperty(ref _sleepText, value); }
    public string BreaksText { get => _breaksText; set => SetProperty(ref _breaksText, value); }
    public string NextReminderText { get => _nextReminderText; set => SetProperty(ref _nextReminderText, value); }
    /// <summary>Mode sombre (mémorisé sur l'appareil).</summary>
    public bool DarkMode
    {
        get => _theme.IsDark;
        set { if (_theme.IsDark != value) { _theme.Set(value); OnPropertyChanged(); } }
    }

    public string Advice { get => _advice; set => SetProperty(ref _advice, value); }

    public ICommand AddWaterCommand { get; }
    public ICommand QuickEntryCommand { get; }
    public ICommand OpenGoalCommand { get; }
    public ICommand LogoutCommand { get; }


    public override void Refresh()
    {
        int ml = _db.TodayWater;
        int goal = _db.GoalMl;

        Greeting = $"Salut {_db.CurrentName} !";
        AmountText = $"{ml / 1000.0:0.0#} L";
        GoalText = $"sur {goal / 1000.0:0.0#} L · touche pour changer";

        int total = Math.Max(1, goal / GlassMl);
        int done = Math.Min(total, ml / GlassMl);
        GlassesText = string.Concat(Enumerable.Repeat("💧", done)) + string.Concat(Enumerable.Repeat("⚪", total - done));

        int left = (int)Math.Ceiling((goal - ml) / (double)GlassMl);
        MessageText = left <= 0 ? "Objectif atteint, bravo !" : $"Plus que {left} verre{(left > 1 ? "s" : "")} !";

        var sleep = _db.LastSleep;
        SleepText = sleep == null ? "—" : Format.Hm(sleep.Hours);
        BreaksText = $"{_db.BreaksOn(DateTime.Today)} / 6";
        NextReminderText = _db.WaterReminder ? $"toutes les {_db.WaterEveryMin} min" : "désactivé";

        _ = LoadWeatherAsync();
        _ = _sync.SyncIfStaleAsync();   // mise à jour discrète, sans bouton
    }

    private void AddWater(string? ml)
    {
        if (int.TryParse(ml, out int value)) _db.AddWater(value);
    }

    private async Task QuickEntryAsync()
    {
        string? choice = await _ui.ChooseAsync("Ajouter de l'eau", "Annuler", "100 ml", "200 ml", "330 ml", "750 ml");
        if (choice != null && int.TryParse(choice.Split(' ')[0], out int value)) _db.AddWater(value);
    }

    private async Task LogoutAsync()
    {
        _reminders.Stop();
        await _api.SignOutAsync();
        _db.Logout();
        await _nav.GoToAsync("//login");
    }

    // ---------- API météo ----------
    private async Task LoadWeatherAsync()
    {
        if (_weatherBusy || (DateTime.Now - _weatherAt).TotalMinutes < 30) return;
        _weatherBusy = true;
        try
        {
            double? t = await _weather.GetTemperatureAsync();
            _weatherAt = t == null ? DateTime.Now.AddMinutes(-25) : DateTime.Now;   // si échec : nouvel essai dans 5 min
            Advice = BuildAdvice(t);
        }
        finally { _weatherBusy = false; }
    }

    private string BuildAdvice(double? t)
    {
        if (t == null) return "Conseil météo indisponible (hors ligne). Objectif habituel conservé.";
        string tip = t >= 30 ? "forte chaleur : bois 500 ml de plus aujourd'hui !"
                   : t >= 25 ? "pense à boire 250 ml de plus aujourd'hui."
                   : "ton objectif habituel suffit.";
        return $"{t:0} °C à {_weather.City} : {tip}";
    }
}
