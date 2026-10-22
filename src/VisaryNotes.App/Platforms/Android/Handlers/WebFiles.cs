using Microsoft.Maui.Handlers;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>
/// Lets document pages show images the app saved in its own storage, such as rendered PDF pages.
/// Only file loads are allowed: scripts still cannot read files (file URL access stays off).
/// Pages open at 100 percent, so the app's bar insets line up with the page, and can be pinch zoomed from there,
/// which PDF and Word pages in Original view need on a phone.
/// </summary>
public static class WebFiles
{
    public static void Register()
    {
        WebViewHandler.Mapper.AppendToMapping("VisaryFiles", (handler, _) =>
        {
            var settings = handler.PlatformView.Settings;
            settings.AllowFileAccess = true;
            // Never shrink a page to fit content wider than the screen; the page wraps instead.
            settings.LoadWithOverviewMode = false;
            settings.SetSupportZoom(true);
            settings.BuiltInZoomControls = true;
            settings.DisplayZoomControls = false;
#pragma warning disable CA1422 // Both default to false; set explicitly so older WebViews behave the same.
            settings.AllowFileAccessFromFileURLs = false;
            settings.AllowUniversalAccessFromFileURLs = false;
#pragma warning restore CA1422
        });
    }
}
