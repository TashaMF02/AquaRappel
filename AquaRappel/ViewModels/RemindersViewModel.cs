using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

/// <summary>Un jour de la semaine (bouton rond L M M J V S D).</summary>
public class DayItem : ObservableObject
{
    private bool _isOn;

    public DayItem(string letter, bool isOn, Action<DayItem> onToggle)
    {
        Letter = letter;
        _isOn = isOn;
        Toggle = new RelayCommand(() => { IsOn = !IsOn; onToggle(this); });
    }

    public string Letter { get; }
    public bool IsOn { get => _isOn; set => SetProperty(ref _isOn, value); }
    public ICommand Toggle { get; }
}

public class RemindersViewModel : BaseViewModel
{
    private static readonly string[] DayNames = { "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim" };

    private readonly IDataService _db;
    private readonly IDialogService _ui;
    private bool _loading;
    private bool _waterOn, _breakOn, _bedOn;
    private string _waterInfo = "", _breakInfo = "", _bedInfo = "";

    public RemindersViewModel(IDataService db, IDialogService ui)
    {
        _db = db;
        _ui = ui;
        EditCommand = new AsyncRelayCommand<string>(EditAsync);
    }

    public bool WaterOn
    {
        get => _waterOn;
        set { if (SetProperty(ref _waterOn, value) && !_loading) _db.SetWaterReminder(value); }
    }

    public bool BreakOn
    {
        get => _breakOn;
        set { if (SetProperty(ref _breakOn, value) && !_loading) _db.SetBreakReminder(value); }
    }

    public bool BedOn
    {
        get => _bedOn;
        set { if (SetProperty(ref _bedOn, value) && !_loading) _db.SetBedReminder(value); }
    }

    public string WaterInfo { get => _waterInfo; set => SetProperty(ref _waterInfo, value); }
    public string BreakInfo { get => _breakInfo; set => SetProperty(ref _breakInfo, value); }
    public string BedInfo { get => _bedInfo; set => SetProperty(ref _bedInfo, value); }

    /// <summary>Touche sur une carte : "water", "break" ou "bed".</summary>
    public ICommand EditCommand { get; }

    public override void Refresh()
    {
        _loading = true;
        WaterOn = _db.WaterReminder;
        BreakOn = _db.BreakReminder;
        BedOn = _db.BedReminder;
        _loading = false;

        WaterInfo = $"Toutes les {Every(_db.WaterEveryMin)} · {Window("water")} · {DaysText(_db.DaysOf("water"))}";
        BreakInfo = $"Toutes les {Every(_db.BreakEveryMin)} · {Window("break")} · {DaysText(_db.DaysOf("break"))}";
        BedInfo = $"À {_db.BedTime} · {DaysText(_db.DaysOf("bed"))}";
    }

    private static string Every(int min)
        => min < 60 ? $"{min} min" : min % 60 == 0 ? $"{min / 60} h" : $"{min / 60} h {min % 60:00}";

    private string Window(string kind)
    {
        var (from, to) = _db.WindowOf(kind);
        return $"{from}–{to}";
    }

    private static TimeSpan Span(string hhmm, TimeSpan fallback) => TimeSpan.TryParse(hhmm, out var t) ? t : fallback;

    private static string DaysText(string days)
    {
        if (days == "1111111") return "tous les jours";
        if (days == "1111100") return "lun–ven";
        var on = DayNames.Where((_, i) => days[i] == '1');
        return string.Join(" ", on);
    }

    private async Task EditAsync(string? kind)
    {
        if (kind is null) return;
        var edit = kind switch
        {
            "water" => new ReminderEdit
            {
                Title = "Boire de l'eau", Icon = "drop",
                EveryMin = _db.WaterEveryMin,
                From = Span(_db.WindowOf("water").From, new TimeSpan(8, 0, 0)), To = Span(_db.WindowOf("water").To, new TimeSpan(21, 0, 0)),
            },
            "break" => new ReminderEdit
            {
                Title = "Pause écran", Icon = "eye",
                EveryMin = _db.BreakEveryMin,
                From = Span(_db.WindowOf("break").From, new TimeSpan(8, 0, 0)), To = Span(_db.WindowOf("break").To, new TimeSpan(21, 0, 0)),
            },
            _ => new ReminderEdit
            {
                Title = "Heure du coucher", Icon = "moon", UseTime = true,
                Time = TimeSpan.TryParse(_db.BedTime, out var t) ? t : new TimeSpan(22, 30, 0),
            },
        };
        edit.Days = _db.DaysOf(kind);

        if (!await _ui.EditReminderAsync(edit)) return;

        _db.SetDays(kind, edit.Days);
        if (kind == "water")
        {
            _db.SetWaterEvery(edit.EveryMin);
            _db.SetWindow("water", $"{edit.From.Hours:00}:{edit.From.Minutes:00}", $"{edit.To.Hours:00}:{edit.To.Minutes:00}");
        }
        else if (kind == "break")
        {
            _db.SetBreakEvery(edit.EveryMin);
            _db.SetWindow("break", $"{edit.From.Hours:00}:{edit.From.Minutes:00}", $"{edit.To.Hours:00}:{edit.To.Minutes:00}");
        }
        else if (kind == "bed") _db.SetBedTime($"{edit.Time.Hours:00}:{edit.Time.Minutes:00}");
        Refresh();
    }
}
