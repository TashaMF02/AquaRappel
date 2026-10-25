using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AquaRappel.Services;
using System.Text.Json;

namespace AquaRappel.Notifications;

/// <summary>Planification (AlarmManager) et affichage des notifications, sans bibliothèque externe.</summary>
public static class NotifHelper
{
    public const string Channel = "aqua_reminders";
    private const string PrefName = "aqua_sched";
    private const string PrefKey = "json";
    private static readonly Random Rng = new();

    private static readonly string[] WaterTexts =
    {
        "Glou glou ! Un verre d'eau, ça te dit ?",
        "Ton corps a soif, hydrate-toi !",
        "Pause hydratation : une gorgée pour la forme !",
        "Petit rappel : l'eau, c'est la vie !",
    };

    private static int Code(string kind) => kind switch { "water" => 1, "break" => 2, "bed" => 3, _ => 9 };

    // ---------- Réglages mémorisés (lisibles même app fermée) ----------
    public static ReminderSchedule? Load(Context ctx)
    {
        string? json = ctx.GetSharedPreferences(PrefName, FileCreationMode.Private)?.GetString(PrefKey, null);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<ReminderSchedule>(json); }
        catch { return null; }
    }

    public static string? LoadRaw(Context ctx)
        => ctx.GetSharedPreferences(PrefName, FileCreationMode.Private)?.GetString(PrefKey, null);

    public static void Save(Context ctx, ReminderSchedule s)
        => ctx.GetSharedPreferences(PrefName, FileCreationMode.Private)?.Edit()?.PutString(PrefKey, JsonSerializer.Serialize(s))?.Apply();

    public static void Clear(Context ctx)
        => ctx.GetSharedPreferences(PrefName, FileCreationMode.Private)?.Edit()?.Remove(PrefKey)?.Apply();

    // ---------- Alarmes ----------
    private static PendingIntent? Pending(Context ctx, string kind, PendingIntentFlags flags)
    {
        var intent = new Intent(ctx, typeof(ReminderReceiver));
        intent.SetAction("aquarappel.REMINDER");
        intent.PutExtra("kind", kind);
        return PendingIntent.GetBroadcast(ctx, Code(kind), intent, flags | PendingIntentFlags.Immutable);
    }

    public static void Cancel(Context ctx, string kind)
    {
        var am = (AlarmManager?)ctx.GetSystemService(Context.AlarmService);
        var pi = Pending(ctx, kind, PendingIntentFlags.NoCreate);
        if (pi != null) { am?.Cancel(pi); pi.Cancel(); }
    }

    public static void SetAlarm(Context ctx, string kind, DateTime when)
    {
        var am = (AlarmManager?)ctx.GetSystemService(Context.AlarmService);
        var pi = Pending(ctx, kind, PendingIntentFlags.UpdateCurrent);
        if (am == null || pi == null) return;
        long ms = new DateTimeOffset(when).ToUnixTimeMilliseconds();
        // Alarme "inexacte" qui peut réveiller le téléphone en veille : pas de permission spéciale, batterie ménagée.
        am.SetAndAllowWhileIdle(AlarmType.RtcWakeup, ms, pi);
    }

    public static void ScheduleNext(Context ctx, string kind, ReminderSchedule s, DateTime from)
    {
        DateTime? next = ReminderPlan.Next(kind, s, from);
        if (next == null) Cancel(ctx, kind);
        else SetAlarm(ctx, kind, next.Value);
    }

    // ---------- Notification ----------
    private static void EnsureChannel(Context ctx)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var nm = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
        var ch = new NotificationChannel(Channel, "Rappels AquaRappel", NotificationImportance.Default)
        {
            Description = "Rappels pour boire, faire une pause écran et aller dormir",
        };
        nm?.CreateNotificationChannel(ch);
    }

    public static void Show(Context ctx, string kind)
    {
        EnsureChannel(ctx);
        (string title, string text) = kind switch
        {
            "water" => ("Pause fraîcheur", WaterTexts[Rng.Next(WaterTexts.Length)]),
            "break" => ("Pause écran", "Regarde au loin pendant vingt secondes."),
            "bed" => ("C'est l'heure de dormir", "Range l'écran : une bonne nuit, une belle journée."),
            _ => ("AquaRappel", "Test réussi : tu recevras tes rappels même application fermée."),
        };

        int icon = ctx.Resources?.GetIdentifier("ic_notif", "drawable", ctx.PackageName) ?? 0;
        if (icon == 0) icon = ctx.ApplicationInfo?.Icon ?? 0;

        var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName ?? "");
        var content = launch == null ? null
            : PendingIntent.GetActivity(ctx, 0, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(ctx, Channel)
            .SetSmallIcon(icon)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(text))
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault);
        if (content != null) builder.SetContentIntent(content);

        try { NotificationManagerCompat.From(ctx).Notify(1000 + Code(kind), builder.Build()); }
        catch (Java.Lang.SecurityException) { /* autorisation de notifier refusée par l'utilisateur */ }
    }
}
