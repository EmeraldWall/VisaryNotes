using AndroidX.Core.View;
using Microsoft.Maui.Handlers;
using AView = Android.Views.View;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>
/// Keeps every page's content clear of the status bar, the navigation or gesture bar, display cutouts and the
/// keyboard (docs/SPEC.md B4, B5). It measures where the page's root layout really sits in the window and pads
/// it by the overlap only. When the system or MAUI's SafeAreaEdges already keeps the page clear the overlap is
/// zero and nothing changes, so the two never add up. On the owner's phone neither the safe area setting nor
/// the earlier keyboard fix took effect, which let reader text run under the mode bar into the gesture area
/// and hid the editor toolbar behind the keyboard.
/// </summary>
public static class SafeInsets
{
    public static void Register()
    {
        PageHandler.Mapper.AppendToMapping("VisarySafeInsets", (handler, view) =>
        {
            if (view is not ContentPage page || handler.PlatformView.Tag is Guard)
                return;
            handler.PlatformView.Tag = new Guard(page, handler.PlatformView);
        });
    }

    private sealed class Guard : Java.Lang.Object, AView.IOnAttachStateChangeListener, global::Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly WeakReference<ContentPage> _page;
        private readonly AView _view;
        private global::Android.Views.ViewTreeObserver? _observer;
        private Thickness _applied;
        private Thickness? _base;

        public Guard(ContentPage page, AView view)
        {
            _page = new WeakReference<ContentPage>(page);
            _view = view;
            view.AddOnAttachStateChangeListener(this);
            if (view.IsAttachedToWindow)
                OnViewAttachedToWindow(view);
        }

        public void OnViewAttachedToWindow(AView? v)
        {
            _observer = _view.RootView?.ViewTreeObserver;
            _observer?.AddOnGlobalLayoutListener(this);
            OnGlobalLayout();
            // Page transitions move the page without a layout pass, so measure again once they settle.
            _view.PostDelayed(OnGlobalLayout, 400);
        }

        public void OnViewDetachedFromWindow(AView? v)
        {
            if (_observer?.IsAlive == true)
                _observer.RemoveOnGlobalLayoutListener(this);
            _observer = null;
        }

        public void OnGlobalLayout()
        {
            if (!_page.TryGetTarget(out var page) || page.Content is not Layout root
                || root.Handler?.PlatformView is not AView content || !content.IsAttachedToWindow || content.Height == 0)
                return;
            var window = content.RootView;
            var insets = window is null ? null : ViewCompat.GetRootWindowInsets(window);
            if (window is null || insets is null)
                return;

            if (insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout()) is not { } bars
                || insets.GetInsets(WindowInsetsCompat.Type.Ime()) is not { } ime)
                return;
            var at = new int[2];
            content.GetLocationInWindow(at);
            // Padding does not move the layout's own edges, so this measurement is stable once applied.
            // Only the top and bottom edges are guarded: those hold the bars and the keyboard. Side insets are
            // left to the system, because a page slides sideways while it opens.
            var density = content.Resources?.DisplayMetrics?.Density ?? 1;
            var top = at[1];
            var bottom = at[1] + content.Height;

            var need = new Thickness(
                0,
                Math.Max(0, bars.Top - top) / density,
                0,
                Math.Max(0, bottom - (window.Height - Math.Max(bars.Bottom, ime.Bottom))) / density);

            if (Near(need, _applied))
                return;
            _base ??= root.Padding;
            _applied = need;
            var b = _base.Value;
            root.Padding = new Thickness(b.Left + need.Left, b.Top + need.Top, b.Right + need.Right, b.Bottom + need.Bottom);
        }

        private static bool Near(Thickness a, Thickness b) =>
            Math.Abs(a.Left - b.Left) < 0.5 && Math.Abs(a.Top - b.Top) < 0.5 && Math.Abs(a.Right - b.Right) < 0.5 && Math.Abs(a.Bottom - b.Bottom) < 0.5;
    }
}
