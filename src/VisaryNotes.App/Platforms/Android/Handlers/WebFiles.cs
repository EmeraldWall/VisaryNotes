using Microsoft.Maui.Handlers;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>
/// Lets document pages show images the app saved in its own storage, such as rendered PDF pages.
/// Only file loads are allowed: scripts still cannot read files (file URL access stays off).
/// </summary>
public static class WebFiles
{
    public static void Register()
    {
        WebViewHandler.Mapper.AppendToMapping("VisaryFiles", (handler, _) =>
        {
            var settings = handler.PlatformView.Settings;
            settings.AllowFileAccess = true;
#pragma warning disable CA1422 // Both default to false; set explicitly so older WebViews behave the same.
            settings.AllowFileAccessFromFileURLs = false;
            settings.AllowUniversalAccessFromFileURLs = false;
#pragma warning restore CA1422
        });
    }
}
