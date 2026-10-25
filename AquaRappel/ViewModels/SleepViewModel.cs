using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

public class SleepViewModel : BaseViewModel
{
    private readonly IDataService _db;
    private readonly IDialogService _ui;
    private TimeSpan _bed = new TimeSpan(22, 30, 0);
    private TimeSpan _wake = new TimeSpan(6, 30, 0);
    private string _quality = "Bof";

    public SleepViewModel(IDataService db, IDialogService ui)
    {
        _db = db;
        _ui = ui;
        SetQualityCommand = new RelayCommand<string>(q => Quality = q ?? "Bof");
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public TimeSpan BedTime
    {
        get => _bed;
        set { if (SetProperty(ref _bed, value)) OnPropertyChanged(nameof(DurationText)); }
    }

    public TimeSpan WakeTime
    {
        get => _wake;
        set { if (SetProperty(ref _wake, value)) OnPropertyChanged(nameof(DurationText)); }
    }

    public string Quality { get => _quality; set => SetProperty(ref _quality, value); }

    private double Hours
    {
        get
        {
            TimeSpan d = WakeTime - BedTime;
            if (d < TimeSpan.Zero) d += TimeSpan.FromHours(24);   // nuit à cheval sur minuit
            return d.TotalHours;
        }
    }

    public string DurationText => Format.Hm(Hours);

    public ICommand SetQualityCommand { get; }
    public ICommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        _db.SaveSleep(Hours, Quality);
        await _ui.AlertAsync("Nuit enregistrée", $"Qualité du sommeil : {Quality}.", "Parfait", "moon", DurationText);
    }
}
