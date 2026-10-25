using System.Windows.Input;

namespace AquaRappel.Views;

/// <summary>
/// Boîte de dialogue aquarelle (fenêtre modale transparente).
/// Elle est affichée par DialogService et renvoie "accept", "cancel",
/// l'option choisie, ou null (bouton retour).
/// </summary>
public partial class DialogPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public Task<string?> Result => _tcs.Task;
    public string[] Options { get; }
    public ICommand OptionCommand { get; }

    public DialogPage(string icon, string title, string message, string? accept,
                      string? cancel, string[]? options = null, string? highlight = null)
    {
        Options = options ?? Array.Empty<string>();
        OptionCommand = new Command<string>(o => _tcs.TrySetResult(o));
        InitializeComponent();
        BindingContext = this;

        IconImage.Source = $"dlg_{icon}.png";
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        MessageLabel.IsVisible = !string.IsNullOrEmpty(message);
        HighlightLabel.Text = highlight;
        HighlightLabel.IsVisible = !string.IsNullOrEmpty(highlight);
        OptionsLayout.IsVisible = Options.Length > 0;
        AcceptLabel.Text = accept;
        AcceptLabel.IsVisible = !string.IsNullOrEmpty(accept);
        CancelLabel.Text = cancel;
        CancelLabel.IsVisible = !string.IsNullOrEmpty(cancel);
    }

    private void OnAccept(object? sender, TappedEventArgs e) => _tcs.TrySetResult("accept");
    private void OnCancel(object? sender, TappedEventArgs e) => _tcs.TrySetResult("cancel");

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return true;
    }
}
