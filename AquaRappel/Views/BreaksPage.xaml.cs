namespace AquaRappel.Views;

public partial class BreaksPage : ContentPage
{
    public BreaksPage()
    {
        InitializeComponent();
    }

    private async void OnBreak(object sender, EventArgs e)
        => await DisplayAlert("Pause", "Bravo, pause enregistrée (exemple).", "OK");
}
