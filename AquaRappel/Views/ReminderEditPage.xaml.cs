using AquaRappel.ViewModels;

namespace AquaRappel.Views;

public partial class ReminderEditPage : ContentPage
{
    private readonly ReminderEditViewModel _vm;

    public ReminderEditPage(ReminderEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        _vm.Cancel();
        return true;
    }
}
