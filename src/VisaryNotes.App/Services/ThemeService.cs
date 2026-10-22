using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Services;

/// <summary>
/// Pushes the active theme into the application resources so every DynamicResource updates at once
/// (docs/SPEC.md 5.2). Colours come from tools/design/themes.json through the core library.
/// </summary>
public sealed class ThemeService
{
    private readonly SettingsService _settings;

    public ThemeService(SettingsService settings)
    {
        _settings = settings;
        Current = ThemeCatalog.Get(settings.ThemeName);
    }

    public event EventHandler? Changed;

    public Theme Current { get; private set; }

    public bool Solid => _settings.ReduceTransparency || Current.IsHighContrast;

    public void Apply(string? name = null)
    {
        if (name is not null)
            _settings.ThemeName = name;
        Current = ThemeCatalog.Get(_settings.ThemeName);
        var r = Application.Current!.Resources;
        var t = Current;

        r["Paper"] = C(t.Paper);
        r["Desk"] = C(t.Desk);
        r["Ink"] = C(t.Ink);
        r["Soft"] = C(t.Soft);
        r["Glass"] = C(t.Glass);
        r["Accent"] = C(t.Accent);
        r["AccentText"] = C(t.AccentText);
        r["OnAccent"] = C(t.OnAccent);
        r["Highlight"] = C(t.Highlight);
        r["Selection"] = C(t.Selection);
        r["Line"] = C(t.Line);
        r["Fill"] = C(t.Ink).WithAlpha(t.IsDark ? 0.10f : 0.06f);

        // The concave glass: clear centre, denser rim, shadow under the top edge, light catch on the bottom edge.
        var glass = C(t.Glass);
        r["GlassCore"] = Solid ? C(t.Paper) : glass.WithAlpha((float)t.GlassAlpha);
        r["GlassRim"] = Solid ? C(t.Paper) : glass.WithAlpha((float)Math.Min(1, t.GlassAlpha + t.GlassRimExtra));
        // Native glass has no backdrop blur yet, so where it covers content it uses the adaptive maximum,
        // the tint the design already sets for busy backdrops (docs/DESIGN.md 4.2.1).
        r["GlassDenseCore"] = Solid ? C(t.Paper) : glass.WithAlpha((float)t.GlassAlphaMax);
        r["GlassDenseRim"] = Solid ? C(t.Paper) : glass.WithAlpha((float)Math.Min(1, t.GlassAlphaMax + 0.1));
        // Bars that sit over content (top strip, mode bar, toolbar) take a tint between paper and desk, so they
        // read as bars and never vanish into the page. The glass colour alone is almost the paper colour in the
        // light themes, which left bars looking transparent on phones (docs/SPEC.md B1, B2).
        var bar = Mix(C(t.Paper), C(t.Desk), 0.7f);
        r["Bar"] = t.IsHighContrast ? C(t.Paper) : Solid ? bar : bar.WithAlpha(BarAlpha);
        r["Clear"] = Colors.Transparent;
        r["BarEdge"] = t.IsHighContrast ? C(t.Line) : C(t.Line).WithAlpha(t.IsDark ? 0.7f : 0.55f);
        r["EdgeLo"] = t.IsHighContrast ? C(t.Line) : t.IsDark ? Colors.Black.WithAlpha(0.6f) : Colors.Black.WithAlpha(0.22f);
        r["EdgeHi"] = t.IsHighContrast ? C(t.Line) : t.IsDark ? Colors.White.WithAlpha(0.2f) : Colors.White.WithAlpha(0.95f);

        foreach (var key in new[] { "Paper", "Desk", "Ink", "Soft", "Accent", "AccentText", "OnAccent", "Line", "Fill", "BarEdge" })
            r[key + "Brush"] = new SolidColorBrush((Color)r[key]);

        Application.Current.UserAppTheme = t.IsDark ? AppTheme.Dark : AppTheme.Light;
        ApplySystemBars();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bars stay faintly translucent; text under them shows at most this faintly (tools/design/contrast.py).</summary>
    public const float BarAlpha = 0.94f;

    public static Color C(string hex) => Color.FromArgb(hex);

    private static Color Mix(Color a, Color b, float t) =>
        new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t);

    public static Color Res(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c ? c : Colors.Magenta;

    /// <summary>Status and navigation bar icons follow the theme's polarity so they stay legible.</summary>
    public void ApplySystemBars()
    {
#if ANDROID
        var window = Platform.CurrentActivity?.Window;
        if (window is null)
            return;
        var bars = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView!)!;
        bars.AppearanceLightStatusBars = !Current.IsDark;
        bars.AppearanceLightNavigationBars = !Current.IsDark;
        // Before Android 15 apps are not drawn behind the system bars, so the bars take the paper colour.
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            var paper = Microsoft.Maui.Platform.ColorExtensions.ToPlatform(C(Current.Paper));
#pragma warning disable CA1422 // Only reached below Android 15.
            window.SetStatusBarColor(paper);
            window.SetNavigationBarColor(paper);
#pragma warning restore CA1422
        }
#endif
    }
}
