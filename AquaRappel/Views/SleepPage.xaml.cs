using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class SleepPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public SleepPage(SleepViewModel vm)
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
