using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

public class BarItem
{
    public double Height { get; init; }
    public Color Color { get; init; } = Colors.Gray;
    public string Day { get; init; } = "";
}

public class StatsViewModel : BaseViewModel
{
    private static readonly Color Dark = Color.FromArgb("#0D47A1");
    private static readonly Color Light = Color.FromArgb("#4FC3F7");

    private readonly IDataService _db;
    private List<BarItem> _bars = new();
    private string _avgText = "";
    private string _goalHint = "";
    private string _sleepAvgText = "—";
    private string _breaksCountText = "0";
    private string _goalDaysText = "";
    private double _goalProgress;
    private string _goalMessage = "";

    public StatsViewModel(IDataService db)
    {
        _db = db;
        _db.DataChanged += Refresh;
    }

    public List<BarItem> Bars { get => _bars; set => SetProperty(ref _bars, value); }
    public string AvgText { get => _avgText; set => SetProperty(ref _avgText, value); }
    public string GoalHint { get => _goalHint; set => SetProperty(ref _goalHint, value); }
    public string SleepAvgText { get => _sleepAvgText; set => SetProperty(ref _sleepAvgText, value); }
    public string BreaksCountText { get => _breaksCountText; set => SetProperty(ref _breaksCountText, value); }
    public string GoalDaysText { get => _goalDaysText; set => SetProperty(ref _goalDaysText, value); }
    public double GoalProgress { get => _goalProgress; set => SetProperty(ref _goalProgress, value); }
    public string GoalMessage { get => _goalMessage; set => SetProperty(ref _goalMessage, value); }

    public override void Refresh()
    {
        var days = _db.LastDays(7);
        int goal = _db.GoalMl;
        int max = Math.Max(goal, days.Max(d => d.Ml));
        const string letters = "DLMMJVS";   // dimanche = 0

        Bars = days.Select(d => new BarItem
        {
            Height = Math.Max(4, d.Ml / (double)max * 140),
            Color = d.Ml >= goal ? Dark : Light,
            Day = letters[(int)d.Day.DayOfWeek].ToString()
        }).ToList();

        AvgText = $"Moyenne {days.Average(d => d.Ml) / 1000.0:0.0#} L";
        GoalHint = $"Objectif : {goal / 1000.0:0.0#} L par jour (barres foncées = atteint)";

        double? sleep = _db.AvgSleepLast7;
        SleepAvgText = sleep == null ? "—" : Format.Hm(sleep.Value);
        BreaksCountText = _db.BreaksLast7.ToString();

        int reached = days.Count(d => d.Ml >= goal);
        GoalDaysText = $"{reached} jour{(reached > 1 ? "s" : "")} sur 7";
        GoalProgress = reached / 7.0;
        GoalMessage = reached >= 5 ? "Super habitude, bravo !" : $"Encore {5 - reached} jour(s) pour une super habitude !";
    }
}
