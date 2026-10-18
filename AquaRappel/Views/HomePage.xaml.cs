namespace AquaRappel.Views;

public partial class HomePage : ContentPage
{
    private int _ml = 1200;              // données d'exemple
    private const int GoalMl = 2000;
    private const int GlassMl = 250;
    private const int ReminderMinutes = 60;   // rappel automatique toutes les 60 min (tant que l'appli est ouverte)

    private static readonly string[] Messages =
    {
        "Glou glou ! Un verre d'eau, ça te dit ? 💧",
        "Ton corps a soif, hydrate-toi ! 🥤",
        "Pause hydratation : une gorgée pour la forme ! 💪",
        "Petit rappel : l'eau, c'est la vie ! 🌊",
    };
    private static readonly Random Rng = new();

    public HomePage()
    {
        InitializeComponent();
        Refresh();

        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(ReminderMinutes);
        timer.Tick += async (s, e) => await ShowReminder();
        timer.Start();
    }

    private void Refresh()
    {
        AmountLabel.Text = $"{_ml / 1000.0:0.0#} L";
        GoalLabel.Text = $"sur {GoalMl / 1000.0:0.0#} L · touche pour changer 🎯";

        int total = GoalMl / GlassMl;                        // 8 verres
        int done = Math.Min(total, _ml / GlassMl);
        GlassesLabel.Text = string.Concat(Enumerable.Repeat("💧", done))
                          + string.Concat(Enumerable.Repeat("⚪", total - done));

        int left = (int)Math.Ceiling((GoalMl - _ml) / (double)GlassMl);
        MsgLabel.Text = left <= 0 ? "Objectif atteint, bravo ! 🎉" : $"Plus que {left} verre{(left > 1 ? "s" : "")} !";
    }

    private void OnAdd(object sender, EventArgs e)
    {
        _ml += int.Parse(((Button)sender).CommandParameter.ToString()!);
        Refresh();
    }

    private async void OnQuickEntry(object sender, EventArgs e)
    {
        string? choice = await DisplayActionSheet("Ajouter de l'eau 💧", "Annuler", null, "100 ml", "200 ml", "330 ml", "750 ml");
        if (choice is null || choice == "Annuler") return;
        _ml += int.Parse(choice.Split(' ')[0]);
        Refresh();
    }

    private async void OnTestReminder(object sender, EventArgs e) => await ShowReminder();

    private async Task ShowReminder()
    {
        bool drank = await DisplayAlert("💧 Rappel AquaRappel", Messages[Rng.Next(Messages.Length)], "J'ai bu ! 🥳", "Plus tard");
        if (drank) { _ml += GlassMl; Refresh(); }
    }

    private async void OnOpenGoal(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("goal");
}