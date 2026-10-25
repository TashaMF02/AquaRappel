using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class GoalPage : ContentPage
{
    private readonly BaseViewModel _vm;

    public GoalPage(GoalViewModel vm)
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
