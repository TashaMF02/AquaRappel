using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class RemindersPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public RemindersPage(RemindersViewModel vm)
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
