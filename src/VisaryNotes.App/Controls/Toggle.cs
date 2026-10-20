using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Controls;

/// <summary>
/// On and off switch in the app's own shape: a 2dp ink outlined track with a square knob, filled with the
/// accent when on (docs/DESIGN.md 4.4). Replaces the rounded Android switch. State is shown by fill and knob
/// position, never by colour alone.
/// </summary>
public sealed class Toggle : ContentView
{
    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(Toggle), false, BindingMode.TwoWay,
            propertyChanged: (b, _, n) => ((Toggle)b).Changed((bool)n));

    private readonly Border _track;
    private readonly BoxView _knob;

    public event EventHandler<ToggledEventArgs>? Toggled;

    public Toggle()
    {
        var radius = ThemeCatalog.Token("radius", "r-1");
        _knob = new BoxView
        {
            WidthRequest = 20,
            HeightRequest = 20,
            CornerRadius = radius,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(ThemeCatalog.Token("space", "sp-1"), 0)
        };
        _track = new Border
        {
            WidthRequest = 56,
            HeightRequest = 32,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = radius },
            Content = _knob,
            VerticalOptions = LayoutOptions.Center
        };
        // A 48dp touch target around the 32dp track.
        Content = new Grid { HeightRequest = ThemeCatalog.Token("size", "h-control"), Children = { _track } };
        GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => IsToggled = !IsToggled) });
        Loaded += (_, _) =>
        {
            if (Handler?.MauiContext?.Services.GetService<ThemeService>() is { } themes)
                themes.Changed += (_, _) => Paint(false);
            Paint(false);
        };
    }

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    private void Changed(bool on)
    {
        Paint(true);
        Toggled?.Invoke(this, new ToggledEventArgs(on));
    }

    private void Paint(bool animate)
    {
        var on = IsToggled;
        var ink = ThemeService.Res("Ink");
        _track.Stroke = new SolidColorBrush(on ? ThemeService.Res("Accent") : ink);
        _track.BackgroundColor = on ? ThemeService.Res("Accent") : Colors.Transparent;
        _knob.Color = on ? ThemeService.Res("OnAccent") : ink;
        var x = on ? 56 - 4 - 20 - 2 * ThemeCatalog.Token("space", "sp-1") : 0;
        if (animate)
            _ = _knob.TranslateToAsync(x, 0, 120, Easing.CubicOut);
        else
            _knob.TranslationX = x;
        SemanticProperties.SetDescription(this, on ? "On" : "Off");
    }
}
