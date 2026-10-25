using Android.App;
using Android.Content;
using AquaRappel.Services;

namespace AquaRappel.Notifications;

/// <summary>Reçoit l'alarme (même application fermée), affiche la notification et programme la suivante.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;
        string kind = intent.GetStringExtra("kind") ?? "";
        if (kind == "test") { NotifHelper.Show(context, "test"); return; }

        ReminderSchedule? s = NotifHelper.Load(context);
        if (s == null || !ReminderPlan.IsOn(kind, s)) return;   // déconnecté ou rappel désactivé

        NotifHelper.Show(context, kind);
        NotifHelper.ScheduleNext(context, kind, s, DateTime.Now);
    }
}

/// <summary>Après un redémarrage du téléphone (ou une mise à jour), les alarmes sont reprogrammées.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        ReminderSchedule? s = NotifHelper.Load(context);
        if (s == null) return;
        foreach (string kind in ReminderPlan.Kinds)
            NotifHelper.ScheduleNext(context, kind, s, DateTime.Now);
    }
}
