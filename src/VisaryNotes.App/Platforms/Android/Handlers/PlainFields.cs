using Microsoft.Maui.Handlers;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>
/// Removes the Android underline and inner padding from text fields. The app draws its own field border
/// (Styles.xaml, Field), so the platform decoration would double it.
/// </summary>
public static class PlainFields
{
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("VisaryPlain", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });
        EditorHandler.Mapper.AppendToMapping("VisaryPlain", (handler, _) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetPadding(0, 0, 0, 0);
        });
    }
}
