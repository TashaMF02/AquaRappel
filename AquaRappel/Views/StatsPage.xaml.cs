using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class StatsPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public StatsPage(StatsViewModel vm)
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
