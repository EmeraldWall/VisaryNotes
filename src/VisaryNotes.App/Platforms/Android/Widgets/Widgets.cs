using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace VisaryNotes.App.Platforms.Android.Widgets;

/// <summary>
/// Home screen widgets (docs/SPEC.md 9). Each tap opens MainActivity with an action extra that
/// App.RunActionAsync handles. Widget state lives in its own SharedPreferences file so a widget can
/// draw without starting the MAUI app.
/// </summary>
public static class WidgetActions
{
    public const string Extra = "visary_action";
    private const string Store = "visary_widgets";

    public static PendingIntent Open(Context context, string action, int requestCode)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetAction("com.visary.notes." + action);
        intent.PutExtra(Extra, action);
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        return PendingIntent.GetActivity(context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    /// <summary>Called by the reader when progress changes. Refreshes the Continue Reading widgets.</summary>
    public static void PublishReading(string title, double progress)
    {
        var context = global::Android.App.Application.Context;
        var prefs = context.GetSharedPreferences(Store, FileCreationMode.Private)!;
        var edit = prefs.Edit()!;
        edit.PutString("title", title);
        edit.PutInt("percent", (int)Math.Round(Math.Clamp(progress, 0, 1) * 100));
        edit.Apply();
        ContinueReadingWidget.UpdateAll(context);
    }

    internal static (string? Title, int Percent) ReadState(Context context)
    {
        var prefs = context.GetSharedPreferences(Store, FileCreationMode.Private)!;
        return (prefs.GetString("title", null), prefs.GetInt("percent", 0));
    }
}

[BroadcastReceiver(Label = "Quick capture", Exported = true)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/vn_widget_capture_info")]
public sealed class QuickCaptureWidget : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? manager, int[]? ids)
    {
        if (context is null || manager is null || ids is null)
            return;
        var views = new RemoteViews(context.PackageName, Resource.Layout.vn_widget_capture);
        views.SetOnClickPendingIntent(Resource.Id.vn_new_note, WidgetActions.Open(context, "new_note", 1));
        views.SetOnClickPendingIntent(Resource.Id.vn_new_checklist, WidgetActions.Open(context, "new_checklist", 2));
        views.SetOnClickPendingIntent(Resource.Id.vn_open_doc, WidgetActions.Open(context, "open_file", 3));
        manager.UpdateAppWidget(ids, views);
    }
}

[BroadcastReceiver(Label = "Continue reading", Exported = true)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/vn_widget_reading_info")]
public sealed class ContinueReadingWidget : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? manager, int[]? ids)
    {
        if (context is null || manager is null || ids is null)
            return;
        manager.UpdateAppWidget(ids, Build(context));
    }

    internal static void UpdateAll(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context)!;
        var ids = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(ContinueReadingWidget))));
        if (ids is { Length: > 0 })
            manager.UpdateAppWidget(ids, Build(context));
    }

    private static RemoteViews Build(Context context)
    {
        var (title, percent) = WidgetActions.ReadState(context);
        var views = new RemoteViews(context.PackageName, Resource.Layout.vn_widget_reading);
        if (title is not null)
        {
            views.SetTextViewText(Resource.Id.vn_title, title);
            views.SetProgressBar(Resource.Id.vn_progress, 100, percent, false);
            views.SetTextViewText(Resource.Id.vn_percent, $"{percent}% read");
        }
        views.SetOnClickPendingIntent(Resource.Id.vn_root, WidgetActions.Open(context, title is null ? "open_file" : "continue_reading", 4));
        return views;
    }
}
