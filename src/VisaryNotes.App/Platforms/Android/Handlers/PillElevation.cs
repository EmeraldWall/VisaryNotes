using Android.Graphics;
using Android.Views;
using Microsoft.Maui.Handlers;
using VisaryNotes.App.Controls;
using AView = Android.Views.View;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>
/// Lifts the floating action pill with a native elevation shadow shaped like the pill
/// (docs/DESIGN.md 4.4). The GPU draws it, so no bitmap is allocated per frame.
/// </summary>
public static class PillElevation
{
    public static void Register()
    {
        ContentViewHandler.Mapper.AppendToMapping("VisaryPillElevation", (handler, view) =>
        {
            if (view is not ActionPill)
                return;
            var density = handler.PlatformView.Resources?.DisplayMetrics?.Density ?? 1;
            handler.PlatformView.OutlineProvider = new PillOutline();
            handler.PlatformView.Elevation = 6 * density;
        });
    }

    private sealed class PillOutline : ViewOutlineProvider
    {
        public override void GetOutline(AView? view, Outline? outline)
        {
            if (view is null || outline is null)
                return;
            outline.SetRoundRect(0, 0, view.Width, view.Height, view.Height / 2f);
            // A light shadow, so it does not show as a dark smudge through the clear glass.
            outline.Alpha = 0.35f;
        }
    }
}
