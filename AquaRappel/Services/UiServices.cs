using AquaRappel.ViewModels;
using AquaRappel.Views;

namespace AquaRappel.Services;

/// <summary>Données d'un rappel en cours de modification (fréquence ou heure + jours actifs).</summary>
public class ReminderEdit
{
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "info";
    public bool UseTime { get; set; }          // vrai : heure fixe (coucher) ; faux : fréquence + plage horaire
    public TimeSpan Time { get; set; }
    public int EveryMin { get; set; } = 60;
    public TimeSpan From { get; set; } = new(8, 0, 0);
    public TimeSpan To { get; set; } = new(21, 0, 0);
    public string Days { get; set; } = "1111111";
}

public interface IDialogService
{
    Task AlertAsync(string title, string message, string ok = "OK", string icon = "info", string? highlight = null);
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, string icon = "info");
    Task<string?> ChooseAsync(string title, string cancel, params string[] options);
    Task<bool> EditReminderAsync(ReminderEdit edit);
}

/// <summary>Boîtes de dialogue au thème aquarelle (fenêtres modales) à la place des alertes système.</summary>
public class DialogService : IDialogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private async Task<T> ShowAsync<T>(Page page, Task<T> result)
    {
        await _gate.WaitAsync(); // une seule boîte à la fois
        try
        {
            var nav = Shell.Current.Navigation;
            await nav.PushModalAsync(page, false);
            T value = await result;
            await nav.PopModalAsync(false);
            return value;
        }
        finally { _gate.Release(); }
    }

    private Task<string?> ShowAsync(DialogPage page) => ShowAsync(page, page.Result);

    public Task AlertAsync(string title, string message, string ok = "OK", string icon = "info", string? highlight = null)
        => ShowAsync(new DialogPage(icon, title, message, ok, null, null, highlight));

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, string icon = "info")
        => await ShowAsync(new DialogPage(icon, title, message, accept, cancel)) == "accept";

    public async Task<string?> ChooseAsync(string title, string cancel, params string[] options)
    {
        string? r = await ShowAsync(new DialogPage("drop", title, "", null, cancel, options));
        return r != null && options.Contains(r) ? r : null;
    }

    public Task<bool> EditReminderAsync(ReminderEdit edit)
    {
        var vm = new ReminderEditViewModel(edit);
        return ShowAsync(new ReminderEditPage(vm), vm.Result);
    }
}

public interface INavigationService
{
    Task GoToAsync(string route);
}

public class NavigationService : INavigationService
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
}
