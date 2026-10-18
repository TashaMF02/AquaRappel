namespace AquaRappel.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void OnRegister(object sender, EventArgs e)
    {
        string error = "";
        if (string.IsNullOrWhiteSpace(NameEntry.Text)) error = "Le prénom est obligatoire.";
        else if (!(EmailEntry.Text ?? "").Contains("@")) error = "Adresse email invalide.";
        else if ((PasswordEntry.Text ?? "").Length < 8) error = "Mot de passe : 8 caractères minimum.";

        FormError.Text = error;
        FormError.IsVisible = error != "";
        if (error == "") await Shell.Current.GoToAsync("//main/home");
    }

    private async void OnGoLogin(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//login");
}
