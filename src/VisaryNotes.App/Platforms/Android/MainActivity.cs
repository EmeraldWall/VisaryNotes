using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using VisaryNotes.App.Platforms.Android.Widgets;

namespace VisaryNotes.App;

/// <summary>
/// Receives files from other apps through Open with and Share (docs/SPEC.md 7.7) and actions from
/// the home screen widgets and launcher shortcuts.
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, Exported = true, WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataSchemes = ["content", "file"],
    DataMimeTypes = ["text/markdown", "text/x-markdown", "text/plain", "text/rtf", "application/rtf", "application/vnd.oasis.opendocument.text", "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"])]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault],
    DataMimeTypes = ["text/markdown", "text/x-markdown", "text/plain", "text/rtf", "application/rtf", "application/vnd.oasis.opendocument.text", "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"])]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        IPlatformApplication.Current?.Services.GetService<Services.ThemeService>()?.ApplySystemBars();
        // A restored activity already handled its launch intent.
        if (savedInstanceState is null)
            HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    private void HandleIntent(Intent? intent)
    {
        if (intent is null)
            return;

        var action = intent.GetStringExtra(WidgetActions.Extra);
        if (action is not null)
        {
            _ = App.RunActionAsync(action);
            return;
        }

        global::Android.Net.Uri? uri = intent.Action switch
        {
            Intent.ActionView => intent.Data,
            Intent.ActionSend => OperatingSystem.IsAndroidVersionAtLeast(33)
                ? (global::Android.Net.Uri?)intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(global::Android.Net.Uri)))
#pragma warning disable CA1422 // Used only below Android 13.
                : (global::Android.Net.Uri?)intent.GetParcelableExtra(Intent.ExtraStream),
#pragma warning restore CA1422
            _ => null
        };

        if (uri is null)
        {
            // Shared plain text with no file becomes a new note.
            var text = intent.Action == Intent.ActionSend ? intent.GetStringExtra(Intent.ExtraText) : null;
            if (!string.IsNullOrWhiteSpace(text))
                _ = App.RunActionAsync("new_note", text);
            return;
        }

        var name = DisplayName(uri) ?? uri.LastPathSegment ?? "Document.txt";
        _ = Task.Run(async () =>
        {
            try
            {
                // The file is copied into app storage so it stays readable after the sender revokes access.
                using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("The file could not be opened.");
                var copy = new MemoryStream();
                await input.CopyToAsync(copy);
                copy.Position = 0;
                await App.OpenIncomingAsync(copy, name);
            }
            catch (Exception ex)
            {
                await App.ShowErrorAsync("Could not open the file", ex.Message);
            }
        });
    }

    private string? DisplayName(global::Android.Net.Uri uri)
    {
        if (uri.Scheme != "content")
            return null;
        using var cursor = ContentResolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
        return cursor is not null && cursor.MoveToFirst() ? cursor.GetString(0) : null;
    }
}
