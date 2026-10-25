namespace AquaRappel.Services;

public interface ISyncService
{
    string Status { get; }
    event Action? StatusChanged;
    Task SyncNowAsync();
    /// <summary>Synchronise si la dernière tentative date de plus de 10 minutes (à l'ouverture de l'accueil).</summary>
    Task SyncIfStaleAsync();
}

/// <summary>
/// Synchronisation avec le serveur : application "hors ligne d'abord".
/// On n'envoie que s'il y a eu un changement (4 s après, en un seul envoi), jamais en boucle.
/// </summary>
public class SyncService : ISyncService
{
    private readonly IDataService _db;
    private readonly IApiService _api;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _debounce;
    private bool _importing;
    private DateTime _lastAttempt = DateTime.MinValue;
    private string _status = "Pas encore synchronisé";

    public SyncService(IDataService db, IApiService api)
    {
        _db = db;
        _api = api;
        _db.DataChanged += OnDataChanged;
    }

    public event Action? StatusChanged;

    public string Status
    {
        get => _status;
        private set { _status = value; MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke()); }
    }

    private void OnDataChanged()
    {
        if (_importing || !_db.IsLoggedIn) return;
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        CancellationToken token = _debounce.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(4000, token);
                await SyncNowAsync();
            }
            catch (OperationCanceledException) { }
        });
    }

    public Task SyncIfStaleAsync()
        => DateTime.Now - _lastAttempt > TimeSpan.FromMinutes(10) ? SyncNowAsync() : Task.CompletedTask;

    public async Task SyncNowAsync()
    {
        if (!_db.IsLoggedIn) return;
        _lastAttempt = DateTime.Now;
        if (Connectivity.Current.NetworkAccess == NetworkAccess.None)
        {
            Status = "Hors ligne : les données restent sur le téléphone";
            return;
        }

        await _gate.WaitAsync();
        try
        {
            Status = "Synchronisation…";
            var local = await MainThread.InvokeOnMainThreadAsync(_db.ExportSnapshot);
            var merged = await _api.SyncAsync(local);
            if (merged == null)
            {
                Status = "Serveur injoignable ou session expirée";
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _importing = true;
                try { _db.ImportSnapshot(merged); }
                finally { _importing = false; }
            });
            Status = $"Synchronisé à {DateTime.Now:HH:mm}";
        }
        catch
        {
            Status = "Synchronisation impossible pour le moment";
        }
        finally { _gate.Release(); }
    }
}
