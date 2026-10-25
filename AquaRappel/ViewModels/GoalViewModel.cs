using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

public class GoalViewModel : BaseViewModel
{
    private readonly IDataService _db;
    private readonly INavigationService _nav;
    private double _goal = 2.0;
    private string _goalText = "2,0 L";

    public GoalViewModel(IDataService db, INavigationService nav)
    {
        _db = db;
        _nav = nav;
        MinusCommand = new RelayCommand(() => { if (_goal > 0.5) { _goal -= 0.25; Show(); } });
        PlusCommand = new RelayCommand(() => { if (_goal < 5.0) { _goal += 0.25; Show(); } });
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public string GoalText { get => _goalText; set => SetProperty(ref _goalText, value); }

    public ICommand MinusCommand { get; }
    public ICommand PlusCommand { get; }
    public ICommand SaveCommand { get; }

    public override void Refresh()
    {
        _goal = _db.GoalMl / 1000.0;
        Show();
    }

    private void Show() => GoalText = $"{_goal:0.0#} L";

    private async Task SaveAsync()
    {
        _db.SetGoal((int)Math.Round(_goal * 1000));
        await _nav.GoToAsync("..");
    }
}
