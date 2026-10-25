using System.Text.Json;

namespace AquaRappel.Services;

public interface IWeatherService
{
    string City { get; }
    Task<double?> GetTemperatureAsync();
}

/// <summary>API publique Open-Meteo (gratuite, sans clé, HTTPS). Retourne null si hors ligne.</summary>
public class WeatherService : IWeatherService
{
    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    private const string Url = "https://api.open-meteo.com/v1/forecast?latitude=4.05&longitude=9.77&current=temperature_2m";

    public string City => "Douala";

    public async Task<double?> GetTemperatureAsync()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;

            using Stream s = await Http.GetStreamAsync(Url);
            using JsonDocument doc = await JsonDocument.ParseAsync(s);
            double t = doc.RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();
            return t is > -50 and < 60 ? t : null;   // on ignore une valeur absurde
        }
        catch
        {
            return null;   // pas de réseau, délai dépassé, réponse inattendue...
        }
    }
}
