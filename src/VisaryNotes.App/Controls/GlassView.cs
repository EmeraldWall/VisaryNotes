using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;

namespace VisaryNotes.App.Controls;

/// <summary>
/// Clear concave glass (docs/DESIGN.md 4.2.1): a clear centre, a rim 20 points denser, a soft shadow under
/// the top edge and a light catch along the bottom edge. No texture. Becomes solid paper when Reduce
/// transparency is on or a High Contrast theme is active.
/// Over a WebView the page itself draws the blurred frost under this shape (WebChrome), so the view only adds
/// its edges. Over native content there is no blur yet, so the view uses the adaptive maximum tint.
/// </summary>
public class GlassView : ContentView
{
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(GlassView), 0.0, propertyChanged: (b, _, _) => ((GlassView)b).Rebuild());

    public static readonly BindableProperty IsPillProperty =
        BindableProperty.Create(nameof(IsPill), typeof(bool), typeof(GlassView), false, propertyChanged: (b, _, _) => ((GlassView)b).Rebuild());

    public static readonly BindableProperty OverWebProperty =
        BindableProperty.Create(nameof(OverWeb), typeof(bool), typeof(GlassView), false, propertyChanged: (b, _, _) => ((GlassView)b).Rebuild());

    private readonly Border _border = new() { StrokeThickness = 0, Padding = 0 };
    private readonly Grid _layers = new() { IsClippedToBounds = true };
    private readonly ContentView _slot = new();

    public GlassView()
    {
        var topLine = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
        topLine.SetDynamicResource(BoxView.ColorProperty, "EdgeLo");
        var topShade = new BoxView { HeightRequest = 12, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
        var bottomCatch = new BoxView { HeightRequest = 10, VerticalOptions = LayoutOptions.End, InputTransparent = true };
        var bottomLine = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.End, InputTransparent = true };
        bottomLine.SetDynamicResource(BoxView.ColorProperty, "EdgeHi");

        _layers.Add(topShade);
        _layers.Add(bottomCatch);
        _layers.Add(topLine);
        _layers.Add(bottomLine);
        _layers.Add(_slot);
        _border.Content = _layers;
        base.Content = _border;

        _topShade = topShade;
        _bottomCatch = bottomCatch;
        SizeChanged += (_, _) => Rebuild();
        Loaded += (_, _) =>
        {
            Rebuild();
            if (Handler?.MauiContext?.Services.GetService<ThemeService>() is { } themes)
                themes.Changed += (_, _) => Rebuild();
        };
    }

    private readonly BoxView _topShade;
    private readonly BoxView _bottomCatch;

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Fully rounded ends, used only by the floating action pill (docs/DESIGN.md 4.4).</summary>
    public bool IsPill
    {
        get => (bool)GetValue(IsPillProperty);
        set => SetValue(IsPillProperty, value);
    }

    /// <summary>True when the view floats over a page that draws the frost underneath it.</summary>
    public bool OverWeb
    {
        get => (bool)GetValue(OverWebProperty);
        set => SetValue(OverWebProperty, value);
    }

    public new View? Content
    {
        get => _slot.Content;
        set => _slot.Content = value;
    }

    private void Rebuild()
    {
        var radius = IsPill && Height > 0 ? Height / 2 : CornerRadius;
        _border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) };

        if (OverWeb)
            _border.Background = Brush.Transparent;
        else
        {
            var core = ThemeService.Res("GlassDenseCore");
            var rim = ThemeService.Res("GlassDenseRim");
            _border.Background = new RadialGradientBrush(
                [new GradientStop(core, 0.55f), new GradientStop(rim, 1f)],
                new Point(0.5, 0.5), 0.75);
        }

        var lo = ThemeService.Res("EdgeLo");
        var hi = ThemeService.Res("EdgeHi");
        _topShade.Background = new LinearGradientBrush([new GradientStop(lo.WithAlpha(lo.Alpha * 0.6f), 0), new GradientStop(Colors.Transparent, 1)], new Point(0, 0), new Point(0, 1));
        _bottomCatch.Background = new LinearGradientBrush([new GradientStop(Colors.Transparent, 0), new GradientStop(hi.WithAlpha(hi.Alpha * 0.5f), 1)], new Point(0, 0), new Point(0, 1));
    }
}
