using System.Collections.ObjectModel;
using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

/// <summary>Fenêtre de réglage d'un rappel : fréquence libre (heures + minutes), plage horaire et jours actifs.</summary>
public class ReminderEditViewModel : ObservableObject
{
    private readonly ReminderEdit _edit;
    private readonly TaskCompletionSource<bool> _tcs = new();
    private TimeSpan _time, _from, _to;
    private string _hours, _minutes;
    private string _error = "";

    public ReminderEditViewModel(ReminderEdit edit)
    {
        _edit = edit;
        _time = edit.Time;
        _from = edit.From;
        _to = edit.To;
        _hours = (edit.EveryMin / 60).ToString();
        _minutes = (edit.EveryMin % 60).ToString();

        string[] letters = { "L", "M", "M", "J", "V", "S", "D" };
        Days = new ObservableCollection<DayItem>(
            letters.Select((l, i) => new DayItem(l, edit.Days.Length > i && edit.Days[i] == '1', _ => { })));

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
    }

    public Task<bool> Result => _tcs.Task;

    public string Title => _edit.Title;
    public string IconFile => $"dlg_{_edit.Icon}.png";
    public bool UseTime => _edit.UseTime;
    public bool IsFreq => !_edit.UseTime;
    public ObservableCollection<DayItem> Days { get; }

    public string Hours { get => _hours; set => SetProperty(ref _hours, value); }
    public string Minutes { get => _minutes; set => SetProperty(ref _minutes, value); }
    public TimeSpan Time { get => _time; set => SetProperty(ref _time, value); }
    public TimeSpan From { get => _from; set => SetProperty(ref _from, value); }
    public TimeSpan To { get => _to; set => SetProperty(ref _to, value); }

    public string Error
    {
        get => _error;
        set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => Error != "";

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private static int Parse(string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return 0;
        return int.TryParse(s, out int v) && v >= 0 && v <= 9999 ? v : -1;
    }

    private void Save()
    {
        string days = string.Concat(Days.Select(d => d.IsOn ? '1' : '0'));
        if (!days.Contains('1')) { Error = "Choisis au moins un jour."; return; }

        if (!_edit.UseTime)
        {
            int h = Parse(Hours), m = Parse(Minutes);
            if (h < 0 || m < 0) { Error = "Saisis des nombres entiers."; return; }
            int total = h * 60 + m;
            if (total < 1 || total > 1440) { Error = "Choisis entre 1 minute et 24 heures."; return; }
            if (To <= From) { Error = "L'heure de fin doit être après l'heure de début."; return; }
            _edit.EveryMin = total;
            _edit.From = From;
            _edit.To = To;
        }
        else _edit.Time = Time;

        _edit.Days = days;
        _tcs.TrySetResult(true);
    }

    public void Cancel() => _tcs.TrySetResult(false);
}
