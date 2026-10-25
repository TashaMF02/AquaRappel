using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class HomePage : ContentPage
{
    private readonly BaseViewModel _vm;

    public HomePage(HomeViewModel vm)
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
