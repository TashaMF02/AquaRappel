using Android.Content;
using Android.OS;
using AquaRappel.Services;
using System.Text.Json;

namespace AquaRappel.Notifications;

public class AndroidNotificationScheduler : INotificationScheduler
{
    private static Context Ctx => Android.App.Application.Context;

    public Task<bool> EnsurePermissionAsync()
    {
        // Android 13+ : l'utilisateur doit autoriser les notifications (une seule fois).
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return Task.FromResult(true);
        const string perm = "android.permission.POST_NOTIFICATIONS";
        bool granted = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(Ctx, perm) == Android.Content.PM.Permission.Granted;
        if (!granted)
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity != null)
                AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { perm }, 4001);
        }
        return Task.FromResult(granted);
    }

    public void Reschedule(ReminderSchedule schedule, bool force = false)
    {
        string json = JsonSerializer.Serialize(schedule);
        if (!force && json == NotifHelper.LoadRaw(Ctx)) return;   // rien n'a changé : on ne touche pas aux alarmes
        NotifHelper.Save(Ctx, schedule);
        foreach (string kind in ReminderPlan.Kinds)
            NotifHelper.ScheduleNext(Ctx, kind, schedule, DateTime.Now);
    }

    public void CancelAll()
    {
        foreach (string kind in ReminderPlan.Kinds) NotifHelper.Cancel(Ctx, kind);
        NotifHelper.Clear(Ctx);
    }

    public void ScheduleTest(int seconds)
        => NotifHelper.SetAlarm(Ctx, "test", DateTime.Now.AddSeconds(Math.Clamp(seconds, 3, 60)));
}
