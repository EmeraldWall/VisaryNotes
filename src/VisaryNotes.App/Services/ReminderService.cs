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

    /// <summary>
    /// The one way a reminder time is chosen anywhere in the app: the sheet, then permission to notify when a
    /// time was picked, with a notice when notifications are off. Returns null when cancelled.
    /// </summary>
    public static async Task<Controls.BottomSheet.ReminderChoice?> AskAsync(ContentPage page, string task, DateTime? current, View? noticeAbove)
    {
        var choice = await Controls.BottomSheet.PickReminderAsync(page, task, current);
        if (choice?.At is not null && !await EnsureNotificationsAsync())
            _ = Controls.Notice.ShowAsync(page, "Notifications are off, so the time is saved but no reminder will show", above: noticeAbove);
        return choice;
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
