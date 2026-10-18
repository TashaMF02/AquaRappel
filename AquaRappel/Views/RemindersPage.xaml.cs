namespace AquaRappel.Views;

public partial class RemindersPage : ContentPage
{
    public RemindersPage()
    {
        InitializeComponent();
    }

    private void OnToggleDay(object sender, EventArgs e)
    {
        var b = (Button)sender;
        bool on = b.Style == (Style)Application.Current!.Resources["AqDayButtonOn"];
        b.Style = (Style)Application.Current.Resources[on ? "AqDayButton" : "AqDayButtonOn"];
    }

    private async void OnAdd(object sender, EventArgs e)
        => await DisplayAlert("Rappels", "Le formulaire d'ajout sera relié aux données dans l'étape suivante.", "OK");
}
