using Microsoft.Extensions.Logging;
using AquaRappel.Services;
using AquaRappel.ViewModels;
using AquaRappel.Views;

namespace AquaRappel;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("URWBookman-Light.otf", "Bookman");
                fonts.AddFont("URWBookman-Demi.otf", "BookmanDemi");
            });

        // Services (une seule instance pour toute l'application)
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<ISecurityService, SecurityService>();
        builder.Services.AddSingleton<IDataService, DataService>();
        builder.Services.AddSingleton<IWeatherService, WeatherService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IApiService, ApiService>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
#if ANDROID
        builder.Services.AddSingleton<INotificationScheduler, AquaRappel.Notifications.AndroidNotificationScheduler>();
#else
        builder.Services.AddSingleton<INotificationScheduler, NoopNotificationScheduler>();
#endif
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<ReminderService>();

        // ViewModels
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<RegisterViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddTransient<GoalViewModel>();
        builder.Services.AddSingleton<RemindersViewModel>();
        builder.Services.AddSingleton<SleepViewModel>();
        builder.Services.AddSingleton<BreaksViewModel>();
        builder.Services.AddSingleton<StatsViewModel>();

        // Vues
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<GoalPage>();
        builder.Services.AddTransient<RemindersPage>();
        builder.Services.AddTransient<SleepPage>();
        builder.Services.AddTransient<BreaksPage>();
        builder.Services.AddTransient<StatsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
