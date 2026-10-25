using System.Windows.Input;
using AquaRappel.Helpers;
using AquaRappel.Services;

namespace AquaRappel.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IDataService _db;
    private readonly INavigationService _nav;
    private readonly ReminderService _reminders;
    private readonly IApiService _api;
    private readonly ISyncService _sync;
    private string _email = "";
    private string _password = "";
    private string _error = "";
    private bool _serverVisible;
    private int _logoTaps;
    private DateTime _lastTap = DateTime.MinValue;

    public LoginViewModel(IDataService db, INavigationService nav, ReminderService reminders,
                          IApiService api, ISyncService sync)
    {
        _db = db;
        _nav = nav;
        _reminders = reminders;
        _api = api;
        _sync = sync;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        GoRegisterCommand = new AsyncRelayCommand(() => _nav.GoToAsync("//register"));
        RevealServerCommand = new RelayCommand(() =>
        {
            _logoTaps = (DateTime.Now - _lastTap).TotalSeconds > 2 ? 1 : _logoTaps + 1;
            _lastTap = DateTime.Now;
            if (_logoTaps >= 5) ServerFieldVisible = true;
        });
    }

    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    /// <summary>Réglage réservé au développeur : 5 touches rapides sur le logo pour l'afficher.</summary>
    public bool ServerFieldVisible { get => _serverVisible; set => SetProperty(ref _serverVisible, value); }

    public string ServerUrl
    {
        get => _api.BaseUrl;
        set { _api.BaseUrl = value; OnPropertyChanged(); }
    }
    public string Error
    {
        get => _error;
        set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => Error != "";

    public ICommand LoginCommand { get; }
    public ICommand GoRegisterCommand { get; }
    public ICommand RevealServerCommand { get; }

    public override void Refresh()
    {
        Password = "";
        Error = "";
        ServerFieldVisible = false;
        if (_db.IsLoggedIn) _ = AutoLoginAsync();   // session déjà ouverte (stockée dans le coffre sécurisé)
    }

    private async Task AutoLoginAsync()
    {
        await Task.Delay(150);
        _reminders.Start();
        _ = _sync.SyncNowAsync();
        await _nav.GoToAsync("//main/home");
    }

    private async Task LoginAsync()
    {
        Error = "";
        ApiResult r = await _api.LoginAsync(Email, Password);
        string? error;

        if (r.Unreachable)
        {
            // Serveur absent : on accepte seulement un compte déjà connu sur ce téléphone.
            if (!_db.HasAccount(Email))
            {
                Error = r.Error ?? "Serveur injoignable. Vérifie l'adresse du serveur et ta connexion.";
                return;
            }
            error = _db.Login(Email, Password);
        }
        else if (!r.Ok)
        {
            Error = r.Error ?? "Connexion impossible.";
            return;
        }
        else
        {
            // Serveur OK : on ouvre (ou on crée, sur un nouveau téléphone) le compte local.
            error = _db.HasAccount(Email) ? _db.Login(Email, Password)
                                          : _db.Register(r.Name ?? "toi", Email, Password);
        }

        Error = error ?? "";
        if (error == null)
        {
            Password = "";
            _reminders.Start();
            _ = _sync.SyncNowAsync();
            await _nav.GoToAsync("//main/home");
        }
    }
}

public class RegisterViewModel : BaseViewModel
{
    private readonly IDataService _db;
    private readonly INavigationService _nav;
    private readonly ReminderService _reminders;
    private readonly IApiService _api;
    private readonly ISyncService _sync;
    private string _name = "";
    private string _email = "";
    private string _password = "";
    private string _error = "";

    public RegisterViewModel(IDataService db, INavigationService nav, ReminderService reminders,
                             IApiService api, ISyncService sync)
    {
        _db = db;
        _nav = nav;
        _reminders = reminders;
        _api = api;
        _sync = sync;
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        GoLoginCommand = new AsyncRelayCommand(() => _nav.GoToAsync("//login"));
    }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string Error
    {
        get => _error;
        set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => Error != "";

    public string ServerInfo => $"Serveur : {_api.BaseUrl}";

    public ICommand RegisterCommand { get; }
    public ICommand GoLoginCommand { get; }

    public override void Refresh()
    {
        Password = "";
        Error = "";
        OnPropertyChanged(nameof(ServerInfo));
    }

    private async Task RegisterAsync()
    {
        Error = "";
        ApiResult r = await _api.RegisterAsync(Name, Email, Password);
        if (!r.Ok)
        {
            Error = r.Unreachable
                ? r.Error ?? "Serveur injoignable. Vérifie l'adresse du serveur (page de connexion) et ta connexion."
                : r.Error ?? "Inscription impossible.";
            return;
        }

        string? error = _db.HasAccount(Email) ? _db.Login(Email, Password) : _db.Register(Name, Email, Password);
        Error = error ?? "";
        if (error == null)
        {
            Password = "";
            _reminders.Start();
            _ = _sync.SyncNowAsync();
            await _nav.GoToAsync("//main/home");
        }
    }
}
