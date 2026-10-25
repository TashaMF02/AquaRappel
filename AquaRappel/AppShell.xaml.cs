using AquaRappel.Services;

namespace AquaRappel;

public partial class AppShell : Shell
{
    public AppShell()
    {
        ThemeService.ApplySaved();   // thème clair ou sombre mémorisé
        InitializeComponent();

        // Écran ouvert par-dessus les onglets (depuis la jauge de l'accueil)
        Routing.RegisterRoute("goal", typeof(Views.GoalPage));
    }
}
