namespace VisaryNotes.App.Services;

/// <summary>Task reminders for pages, without platform code in them (docs/SPEC.md 6.4).</summary>
public static class ReminderService
{
    /// <summary>Schedules the next reminder after any change to tasks. Never throws.</summary>
    public static async Task RescheduleAsync()
    {
#if ANDROID
        try
        {
            await Platforms.Android.Reminders.Reminders.RescheduleAsync();
        }
        catch (Exception)
        {
            // A reminder that cannot be scheduled must never break saving a note.
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>Asks once for permission to show notifications (Android 13 and later). True when allowed.</summary>
    public static async Task<bool> EnsureNotificationsAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status == PermissionStatus.Granted;
    }

    /// <summary>True when reminders arrive on the minute; otherwise Android may delay them while the phone is idle.</summary>
    public static bool ExactTimes =>
#if ANDROID
        Platforms.Android.Reminders.Reminders.CanBeExact();
#else
        true;
#endif

    public static void OpenExactTimesSettings()
    {
#if ANDROID
        Platforms.Android.Reminders.Reminders.OpenExactAlarmSettings();
#endif
    }
}
