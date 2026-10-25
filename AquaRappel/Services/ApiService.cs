using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AquaRappel.Models;

namespace AquaRappel.Services;

public record ApiResult(bool Ok, bool Unreachable = false, string? Error = null, string? Name = null);

public interface IApiService
{
    string BaseUrl { get; set; }
    Task<ApiResult> RegisterAsync(string name, string email, string password);
    Task<ApiResult> LoginAsync(string email, string password);
    Task<SyncSnapshot?> SyncAsync(SyncSnapshot local);
    Task SignOutAsync();
}

/// <summary>Client HTTP du serveur AquaRappel (API REST JSON, jeton Bearer stocké dans le coffre sécurisé).</summary>
public class ApiService : IApiService
{
    private const string UrlKey = "server_url";
    private const string TokenKey = "api_token";
    private const string DefaultUrl = "http://10.0.2.2:5080";   // 10.0.2.2 = l'ordinateur, vu depuis l'émulateur Android

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string BaseUrl
    {
        get => Preferences.Default.Get(UrlKey, DefaultUrl);
        set => Preferences.Default.Set(UrlKey, (value ?? "").Trim());
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string path, object? body, bool auth)
    {
        if (!Uri.TryCreate(BaseUrl.TrimEnd('/') + "/" + path, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Adresse du serveur invalide (exemple : http://192.168.1.10:5080).");

        using var req = new HttpRequestMessage(method, uri);
        if (body != null) req.Content = JsonContent.Create(body, options: Json);
        if (auth)
        {
            string? token = await SecureStorage.Default.GetAsync(TokenKey);
            if (token == null) return null;
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await Http.SendAsync(req);
    }

    private static string Message(HttpStatusCode code, string? serverError) => code switch
    {
        HttpStatusCode.Unauthorized => "Email ou mot de passe incorrect.",
        HttpStatusCode.TooManyRequests => "Trop de tentatives. Réessaie dans quelques minutes.",
        _ => serverError ?? "Le serveur a refusé la demande.",
    };

    private async Task<ApiResult> AuthAsync(string path, object body)
    {
        try
        {
            using HttpResponseMessage? r = await SendAsync(HttpMethod.Post, path, body, auth: false);
            if (r == null) return new ApiResult(false, true);
            string text = await r.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            JsonElement root = doc.RootElement;

            if (!r.IsSuccessStatusCode)
            {
                string? err = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var e) ? e.GetString() : null;
                return new ApiResult(false, false, Message(r.StatusCode, err));
            }

            string token = root.GetProperty("token").GetString() ?? "";
            string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            await SecureStorage.Default.SetAsync(TokenKey, token);
            return new ApiResult(true, Name: name);
        }
        catch (InvalidOperationException ex) { return new ApiResult(false, true, ex.Message); }
        catch (Exception) { return new ApiResult(false, true); }   // réseau absent, délai dépassé, réponse illisible
    }

    public Task<ApiResult> RegisterAsync(string name, string email, string password)
        => AuthAsync("api/auth/register", new { name, email, password });

    public Task<ApiResult> LoginAsync(string email, string password)
        => AuthAsync("api/auth/login", new { email, password });

    public async Task<SyncSnapshot?> SyncAsync(SyncSnapshot local)
    {
        try
        {
            using HttpResponseMessage? r = await SendAsync(HttpMethod.Post, "api/sync", local, auth: true);
            if (r == null) return null;
            if (r.StatusCode == HttpStatusCode.Unauthorized)
            {
                SecureStorage.Default.Remove(TokenKey);   // jeton expiré : il faudra se reconnecter
                return null;
            }
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<SyncSnapshot>(Json);
        }
        catch { return null; }
    }

    public async Task SignOutAsync()
    {
        try { using var _ = await SendAsync(HttpMethod.Post, "api/auth/logout", null, auth: true); }
        catch { /* hors ligne : le jeton local est supprimé quand même */ }
        SecureStorage.Default.Remove(TokenKey);
    }
}
