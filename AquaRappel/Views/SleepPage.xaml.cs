namespace AquaRappel.Views;

public partial class SleepPage : ContentPage
{
    public SleepPage()
    {
        InitializeComponent();
    }

    private void OnTimeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Time" || DurationLabel is null) return;
        TimeSpan d = WakePicker.Time - BedPicker.Time;
        if (d < TimeSpan.Zero) d += TimeSpan.FromHours(24);   // nuit à cheval sur minuit
        DurationLabel.Text = $"{(int)d.TotalHours} h {d.Minutes:00}";
    }

    private async void OnSave(object sender, EventArgs e)
        => await DisplayAlert("Sommeil", "Nuit enregistrée (exemple).", "OK");
}
