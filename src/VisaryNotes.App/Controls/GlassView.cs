using Microsoft.Maui.Controls.Shapes;

namespace VisaryNotes.App.Controls;

/// <summary>
/// The app's bar material (docs/DESIGN.md 4.2.1, docs/SPEC.md V1): a tint between paper and desk, faintly
/// translucent, with a hairline edge, so a bar always reads as a bar over the page. Becomes solid when Reduce
/// transparency is on or a High Contrast theme is active (ThemeService, "Bar").
/// The tint is the view's own background, which Android always draws. A gradient brush and later a filled
/// shape inside the bar both failed to draw on a phone, which left the top bar transparent (B1).
/// Pages pad their content so nothing sits under a bar at rest.
/// </summary>
public class GlassView : ContentView
{
    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(GlassView), 0.0, propertyChanged: (b, _, _) => ((GlassView)b).Rebuild());

    public static readonly BindableProperty IsPillProperty =
        BindableProperty.Create(nameof(IsPill), typeof(bool), typeof(GlassView), false, propertyChanged: (b, _, _) => ((GlassView)b).Rebuild());

    private readonly Border _border = new() { StrokeThickness = 0, Padding = 0 };
    private readonly Grid _layers = new();
    private readonly ContentView _slot = new();
    private readonly BoxView _topLine = new() { HeightRequest = 1, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
    private readonly BoxView _bottomLine = new() { HeightRequest = 1, VerticalOptions = LayoutOptions.End, InputTransparent = true };

    public GlassView()
    {
        _topLine.SetDynamicResource(BoxView.ColorProperty, "BarEdge");
        _bottomLine.SetDynamicResource(BoxView.ColorProperty, "BarEdge");
        _layers.Add(_slot);
        _layers.Add(_topLine);
        _layers.Add(_bottomLine);
        _border.Content = _layers;
        base.Content = _border;
        SizeChanged += (_, _) => Rebuild();
        Rebuild();
    }

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

    public new View? Content
    {
        get => _slot.Content;
        set => _slot.Content = value;
    }

    private void Rebuild()
    {
        var radius = IsPill && Height > 0 ? Height / 2 : CornerRadius;
        var rounded = IsPill || radius > 0;
        if (rounded)
        {
            // Rounded shapes clip their own solid fill; the view behind stays clear around the corners.
            SetDynamicResource(BackgroundColorProperty, "Clear");
            _border.SetDynamicResource(Border.BackgroundColorProperty, "Bar");
            _border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(radius) };
            _border.StrokeThickness = 1;
            _border.SetDynamicResource(Border.StrokeProperty, "BarEdgeBrush");
        }
        else
        {
            SetDynamicResource(BackgroundColorProperty, "Bar");
            _border.SetDynamicResource(Border.BackgroundColorProperty, "Clear");
            _border.StrokeShape = new Rectangle();
            _border.StrokeThickness = 0;
        }
        _topLine.IsVisible = !rounded;
        _bottomLine.IsVisible = !rounded;
    }
}
