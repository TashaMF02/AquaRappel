using AquaRappel.Helpers;

namespace AquaRappel.ViewModels;

public abstract class BaseViewModel : ObservableObject
{
    /// <summary>Recharge l'affichage depuis les données.</summary>
    public virtual void Refresh() { }
}
