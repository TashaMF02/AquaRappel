using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class BreaksPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public BreaksPage(BreaksViewModel vm)
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
