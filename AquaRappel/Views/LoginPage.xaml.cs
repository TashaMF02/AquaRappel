namespace AquaRappel.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLogin(object sender, EventArgs e)
    {
        // Validation simple : email contenant @ et mot de passe de 8 caractères minimum
        bool ok = (EmailEntry.Text ?? "").Contains("@") && (PasswordEntry.Text ?? "").Length >= 8;
        EmailError.IsVisible = !ok;
        if (ok) await Shell.Current.GoToAsync("//main/home");
    }

    private async void OnGoRegister(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//register");
}
