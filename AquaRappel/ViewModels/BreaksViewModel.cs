using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

public class BreaksViewModel : BaseViewModel
{
    private readonly IDataService _db;
    private readonly IDialogService _ui;
    private string _countText = "0 sur 6";
    private string _breakFreq = "45";

    public BreaksViewModel(IDataService db, IDialogService ui)
    {
        _db = db;
        _ui = ui;
        SetFreqCommand = new RelayCommand<string>(f =>
        {
            if (int.TryParse(f, out int min)) _db.SetBreakEvery(min);
        });
        BreakCommand = new AsyncRelayCommand(async () =>
        {
            _db.AddBreak();
            await _ui.AlertAsync("Bravo, pause enregistrée", "Tes yeux te remercieront.", "Continuer", "check");
        });
        _db.DataChanged += Refresh;
    }

    public string CountText { get => _countText; set => SetProperty(ref _countText, value); }
    public string BreakFreq { get => _breakFreq; set => SetProperty(ref _breakFreq, value); }

    public ICommand SetFreqCommand { get; }
    public ICommand BreakCommand { get; }

    public override void Refresh()
    {
        CountText = $"{_db.BreaksOn(DateTime.Today)} sur 6";
        BreakFreq = _db.BreakEveryMin.ToString();
    }
}
