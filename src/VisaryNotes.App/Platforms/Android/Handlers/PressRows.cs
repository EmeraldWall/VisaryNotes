using Microsoft.Maui.Handlers;
using VisaryNotes.App.Controls;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>Native click and long click for <see cref="PressRow"/>.</summary>
public static class PressRows
{
    public static void Register()
    {
        LayoutHandler.Mapper.AppendToMapping("VisaryPressRow", (handler, view) =>
        {
            if (view is not PressRow row || handler.PlatformView.Tag is Java.Lang.String)
                return;
            var platform = handler.PlatformView;
            platform.Tag = new Java.Lang.String("visary-press-row");
            // The standard press ripple, drawn over the row so its own background stays.
            var ripple = new global::Android.Util.TypedValue();
            if (platform.Context?.Theme?.ResolveAttribute(global::Android.Resource.Attribute.SelectableItemBackground, ripple, true) == true)
                platform.Foreground = platform.Context.GetDrawable(ripple.ResourceId);
            platform.Clickable = true;
            platform.LongClickable = true;
            platform.Click += (_, _) => row.RaiseTapped();
            platform.LongClick += (_, e) =>
            {
                e.Handled = true;
                row.RaiseLongPressed();
            };
        });
    }
}
