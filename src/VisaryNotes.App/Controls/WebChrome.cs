using System.Globalization;

namespace VisaryNotes.App.Controls;

/// <summary>
/// Keeps a page in a WebView aware of the app's bars, so it pads its text clear of the top and bottom bars and
/// nothing sits under a bar at rest. The bars themselves are native glass (docs/DESIGN.md 4.2.1).
/// </summary>
public sealed class WebChrome
{
    private readonly WebView _web;
    private readonly Func<View?> _top;
    private readonly Func<View?> _bottom;
    private bool _pending;
    private double _lastTop;
    private double _lastBottom;

    /// <summary>While the bars are hidden for immersive reading the page keeps its insets, so the text does not jump.</summary>
    public bool Immersive
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    public WebChrome(WebView web, Func<View?> top, Func<View?> bottom)
    {
        _web = web;
        _top = top;
        _bottom = bottom;
        web.Navigated += (_, _) => Sync();
        web.SizeChanged += (_, _) => Sync();
    }

    /// <summary>Re-sends the layout whenever a tracked view moves, resizes or hides.</summary>
    public void Track(params VisualElement[] views)
    {
        foreach (var v in views)
        {
            v.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is "X" or "Y" or "Width" or "Height" or "IsVisible")
                    Sync();
            };
        }
    }

    /// <summary>Coalesces bursts of layout changes into one script call.</summary>
    public void Sync()
    {
        if (_pending)
            return;
        _pending = true;
        _web.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(30), async () =>
        {
            _pending = false;
            await SendAsync();
        });
    }

    private async Task SendAsync()
    {
        if (_web.Parent is not VisualElement root || !_web.IsVisible || _web.Width <= 0)
            return;

        // Insets are measured relative to the WebView, which can sit below a system inset while the bars are
        // measured from the page root.
        var web = Bounds(_web, root);
        var top = _top() is { } t && Shown(t, root) ? Math.Max(0, Bounds(t, root).Bottom - web.Y) : 0;
        var bottom = _bottom() is { } b && Shown(b, root) ? Math.Max(0, web.Bottom - Bounds(b, root).Top) : 0;
        if (Immersive)
            (top, bottom) = (_lastTop, _lastBottom);
        else
            (_lastTop, _lastBottom) = (top, bottom);
        var script = string.Create(CultureInfo.InvariantCulture,
            $"window.vnChrome && window.vnChrome({Round(top)},{Round(bottom)},{Round(web.Width)})");
        try
        {
            await _web.EvaluateJavaScriptAsync(script);
        }
        catch (Exception)
        {
            // The page may be mid navigation. Navigated triggers another sync.
        }
    }

    private static double Round(double v) => Math.Round(v, 1);

    private static bool Shown(VisualElement v, VisualElement root)
    {
        for (Element? e = v; e is not null && e != root; e = e.Parent)
            if (e is VisualElement ve && !ve.IsVisible)
                return false;
        return true;
    }

    /// <summary>Bounds in the coordinate space of the WebView's parent, which also holds the bars.</summary>
    private static Rect Bounds(VisualElement v, VisualElement root)
    {
        double x = 0, y = 0;
        for (Element? e = v; e is not null && e != root; e = e.Parent)
        {
            if (e is VisualElement ve)
            {
                x += ve.X + ve.TranslationX;
                y += ve.Y + ve.TranslationY;
            }
        }
        return new Rect(x, y, v.Width, v.Height);
    }
}
