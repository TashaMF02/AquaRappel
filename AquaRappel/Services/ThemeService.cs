namespace AquaRappel.Services;

public interface IThemeService
{
    bool IsDark { get; }
    void Set(bool dark);
}

/// <summary>Thème clair / sombre. Le mode sombre économise la batterie sur écran OLED (éco-conception).</summary>
public class ThemeService : IThemeService
{
    private const string Key = "dark_mode";

    private static readonly Dictionary<string, string> Light = new()
    {
        ["AqPrimary"] = "#0D47A1",
        ["AqAccent"] = "#4FC3F7",
        ["AqBg"] = "#F5FAFF",
        ["AqInk"] = "#0B1B2E",
        ["AqMuted"] = "#5A6B7E",
        ["AqDanger"] = "#C62828",
        ["AqLine"] = "#DCE6F2",
        ["AqPale"] = "#E3F2FD",
        ["AqBorder"] = "#8FA3BA",
        ["AqDisabled"] = "#D5DEE9",
        ["AqWhite"] = "#FFFFFF",
        ["AqSun"] = "#FFF8E1",
        ["AqLilac"] = "#EDE7F6",
        ["AqMint"] = "#E0F7F4",
        ["AqPeach"] = "#FFE9E0",
        ["AqWhiteSoft"] = "#CFE3FA",
        ["AqPaper"] = "#FBF7EE",
        ["AqOnPrimary"] = "#FFFFFF",
        ["AqHeaderBg"] = "#0D47A1",
        ["AqTabOn"] = "#0D47A1"
    };

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["AqPrimary"] = "#3B7DD8",
        ["AqAccent"] = "#4FC3F7",
        ["AqBg"] = "#0E1726",
        ["AqInk"] = "#E8F0FA",
        ["AqMuted"] = "#A3B5CC",
        ["AqDanger"] = "#FF8A80",
        ["AqLine"] = "#2A3B58",
        ["AqPale"] = "#1B3050",
        ["AqBorder"] = "#5F7A9E",
        ["AqDisabled"] = "#2A3850",
        ["AqWhite"] = "#16233A",
        ["AqSun"] = "#3A3320",
        ["AqLilac"] = "#2B2A4A",
        ["AqMint"] = "#17393A",
        ["AqPeach"] = "#3D2C28",
        ["AqWhiteSoft"] = "#BBD0EE",
        ["AqPaper"] = "#1B2B45",
        ["AqOnPrimary"] = "#FFFFFF",
        ["AqHeaderBg"] = "#0F2C5C",
        ["AqTabOn"] = "#7FB8FF"
    };

    public bool IsDark => Preferences.Default.Get(Key, false);

    public void Set(bool dark)
    {
        Preferences.Default.Set(Key, dark);
        Apply(dark);
    }

    public static void ApplySaved() => Apply(Preferences.Default.Get(Key, false));

    private static void Apply(bool dark)
    {
        var app = Application.Current;
        if (app == null) return;
        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        foreach (var kv in dark ? Dark : Light)
            app.Resources[kv.Key] = Color.FromArgb(kv.Value);
    }
}
