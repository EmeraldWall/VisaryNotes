namespace VisaryNotes.App.Services;

public enum Handedness
{
    Left,
    Centre,
    Right
}

public enum Density
{
    Comfortable,
    Compact,
    Dense
}

/// <summary>User preferences, stored with MAUI Preferences.</summary>
public sealed class SettingsService
{
    /// <summary>Reading text size in points: the default and the range every screen allows.</summary>
    public const double DefaultReadingFontSize = 17, MinReadingFontSize = 14, MaxReadingFontSize = 28;

    public string ThemeName
    {
        get => Preferences.Get("theme", Core.Theming.ThemeCatalog.DefaultTheme);
        set => Preferences.Set("theme", value);
    }

    public Handedness Hand
    {
        get => Enum.TryParse<Handedness>(Preferences.Get("hand", nameof(Handedness.Centre)), out var h) ? h : Handedness.Centre;
        set => Preferences.Set("hand", value.ToString());
    }

    public Density Density
    {
        get => Enum.TryParse<Density>(Preferences.Get("density", nameof(Density.Compact)), out var d) ? d : Density.Compact;
        set => Preferences.Set("density", value.ToString());
    }

    public bool ReduceTransparency
    {
        get => Preferences.Get("reduceTransparency", false);
        set => Preferences.Set("reduceTransparency", value);
    }

    public double ReadingFontSize
    {
        get => Preferences.Get("readingFontSize", DefaultReadingFontSize);
        set => Preferences.Set("readingFontSize", Math.Clamp(value, MinReadingFontSize, MaxReadingFontSize));
    }

    public bool AllowRemoteImages
    {
        get => Preferences.Get("allowRemoteImages", false);
        set => Preferences.Set("allowRemoteImages", value);
    }

    public double RowHeight => Density switch
    {
        Density.Comfortable => Core.Theming.ThemeCatalog.Token("size", "h-row-comfortable"),
        Density.Dense => Core.Theming.ThemeCatalog.Token("size", "h-row-dense"),
        _ => Core.Theming.ThemeCatalog.Token("size", "h-row")
    };
}
