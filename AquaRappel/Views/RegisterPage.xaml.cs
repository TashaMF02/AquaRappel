using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class RegisterPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public RegisterPage(RegisterViewModel vm)
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
