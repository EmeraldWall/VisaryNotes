using Android.App;
using Android.Content;
using AndroidX.Core.App;
using VisaryNotes.App.Platforms.Android.Widgets;
using VisaryNotes.Core.Data;
using AndroidApp = Android.App.Application;

namespace VisaryNotes.App.Platforms.Android.Reminders;

/// <summary>
/// Task reminders (docs/SPEC.md 6.4). Only the soonest reminder is ever scheduled with the alarm manager. When
/// it fires, every task that came due since the last check is shown, and the next one is scheduled. So nothing
/// has to be tracked per task, and a note edited anywhere just calls <see cref="RescheduleAsync"/>.
/// Times are exact when Android allows exact alarms for the app, and otherwise may arrive a few minutes late
/// while the phone is idle.
/// </summary>
public static class Reminders
{
    private const string Channel = "reminders";
    private const string Store = "visary_reminders";
    private const string LastCheck = "last_check";

    internal static LibraryRepository Repository =>
        IPlatformApplication.Current?.Services.GetService<LibraryRepository>()
        ?? new LibraryRepository(System.IO.Path.Combine(FileSystem.AppDataDirectory, "library.db3"));

    public static async Task RescheduleAsync()
    {
        var context = AndroidApp.Context;
        var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var fire = FireIntent(context);
        alarms.Cancel(fire);
        var next = await Repository.NextReminderAsync(LastChecked(context));
        if (next is not { } at)
            return;
        var ms = new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Local)).ToUnixTimeMilliseconds();
        if (CanBeExact(alarms))
            alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, ms, fire);
        else
            alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, ms, fire);
    }

    public static bool CanBeExact() => CanBeExact((AlarmManager)AndroidApp.Context.GetSystemService(Context.AlarmService)!);

    private static bool CanBeExact(AlarmManager alarms) => !OperatingSystem.IsAndroidVersionAtLeast(31) || alarms.CanScheduleExactAlarms();

    /// <summary>Opens the system page where the user allows exact reminder times (Android 12 and later).</summary>
    public static void OpenExactAlarmSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            return;
        var context = AndroidApp.Context;
        var intent = new Intent(global::Android.Provider.Settings.ActionRequestScheduleExactAlarm, global::Android.Net.Uri.Parse("package:" + context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    /// <summary>Shows the reminders due since the last check, then schedules the next one.</summary>
    internal static async Task FireAsync(Context context)
    {
        var now = DateTime.Now;
        var from = LastChecked(context);
        // After a long time off, only the last day's reminders are shown, not a flood.
        if (from < now.AddDays(-1))
            from = now.AddDays(-1);
        foreach (var r in await Repository.RemindersBetweenAsync(from, now))
            Show(context, r);
        Prefs(context).Edit()!.PutLong(LastCheck, now.Ticks)!.Apply();
        await RescheduleAsync();
    }

    internal static async Task MarkDoneAsync(Context context, int noteId, int line, string text)
    {
        var repo = Repository;
        // The note may have changed since the reminder was shown, so the task is found again by its text.
        var note = await repo.GetNoteAsync(noteId);
        if (note is not null)
        {
            var tasks = NoteTasks.Parse(note.Body);
            var task = tasks.FirstOrDefault(t => t.Line == line && t.Text == text && !t.Done)
                ?? tasks.FirstOrDefault(t => t.Text == text && !t.Done);
            if (task is not null)
                await repo.ToggleTaskAsync(noteId, task.Line);
        }
        NotificationManagerCompat.From(context)?.Cancel(NotificationId(noteId, text));
        await RescheduleAsync();
    }

    private static void Show(Context context, TaskReminder r)
    {
        EnsureChannel(context);
        var id = NotificationId(r.NoteId, r.Text);
        var done = new Intent(context, typeof(ReminderReceiver)).SetAction(ReminderReceiver.ActionDone)!
            .PutExtra("note", r.NoteId).PutExtra("line", r.Line).PutExtra("text", r.Text);
        var donePending = PendingIntent.GetBroadcast(context, id, done, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var notification = new NotificationCompat.Builder(context, Channel)
            .SetSmallIcon(Resource.Drawable.vn_notify)!
            .SetContentTitle(r.Text.Length > 0 ? r.Text : "Task")!
            .SetContentText(r.NoteTitle)!
            .SetWhen(new DateTimeOffset(DateTime.SpecifyKind(r.At, DateTimeKind.Local)).ToUnixTimeMilliseconds())!
            .SetShowWhen(true)!
            .SetCategory(NotificationCompat.CategoryReminder)!
            .SetPriority(NotificationCompat.PriorityHigh)!
            .SetAutoCancel(true)!
            .SetContentIntent(WidgetActions.Open(context, $"open_note:{r.NoteId}", id))!
            .AddAction(0, "Done", donePending)!
            .Build()!;
        try
        {
            NotificationManagerCompat.From(context)?.Notify(id, notification);
        }
        catch (Java.Lang.SecurityException)
        {
            // Notifications are not allowed. The task stays in the Tasks list with its time.
        }
    }

    private static void EnsureChannel(Context context)
    {
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if (manager.GetNotificationChannel(Channel) is not null)
            return;
        manager.CreateNotificationChannel(new NotificationChannel(Channel, "Task reminders", NotificationImportance.High)
        {
            Description = "Reminders for tasks with a time in your notes"
        });
    }

    private static int NotificationId(int noteId, string text) => HashCode.Combine(noteId, text) & 0x7FFFFFFF;

    private static PendingIntent FireIntent(Context context) =>
        PendingIntent.GetBroadcast(context, 0, new Intent(context, typeof(ReminderReceiver)).SetAction(ReminderReceiver.ActionFire),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

    private static ISharedPreferences Prefs(Context context) => context.GetSharedPreferences(Store, FileCreationMode.Private)!;

    /// <summary>Reminders at or before this moment were already shown. On first use, now.</summary>
    private static DateTime LastChecked(Context context)
    {
        var ticks = Prefs(context).GetLong(LastCheck, 0);
        if (ticks > 0)
            return new DateTime(ticks);
        var now = DateTime.Now;
        Prefs(context).Edit()!.PutLong(LastCheck, now.Ticks)!.Apply();
        return now;
    }
}

[BroadcastReceiver(Exported = false)]
public sealed class ReminderReceiver : BroadcastReceiver
{
    public const string ActionFire = "visary.reminder.FIRE";
    public const string ActionDone = "visary.reminder.DONE";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                if (intent.Action == ActionDone)
                    await Reminders.MarkDoneAsync(context, intent.GetIntExtra("note", 0), intent.GetIntExtra("line", -1), intent.GetStringExtra("text") ?? "");
                else
                    await Reminders.FireAsync(context);
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}

/// <summary>Alarms do not survive a restart, so the next reminder is scheduled again after boot.</summary>
[BroadcastReceiver(Exported = true, Enabled = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, "android.intent.action.TIME_SET", "android.intent.action.TIMEZONE_CHANGED"])]
public sealed class ReminderBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await Reminders.RescheduleAsync();
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
