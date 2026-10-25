using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class LoginPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh();
    }
}
