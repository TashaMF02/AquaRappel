namespace AquaRappel.Views;

public partial class GoalPage : ContentPage
{
    public GoalPage()
    {
        InitializeComponent();
    }

    private double _goal = 2.0;

    private void Show() => GoalLabel.Text = $"{_goal:0.0} L";
    private void OnMinus(object sender, EventArgs e) { if (_goal > 0.5) _goal -= 0.25; Show(); }
    private void OnPlus(object sender, EventArgs e)  { if (_goal < 5.0) _goal += 0.25; Show(); }
    private async void OnSave(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
