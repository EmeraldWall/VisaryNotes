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
    public event EventHandler? Changed;

    public string ThemeName
    {
        get => Preferences.Get("theme", Core.Theming.ThemeCatalog.DefaultTheme);
        set => Set("theme", value);
    }

    public Handedness Hand
    {
        get => Enum.TryParse<Handedness>(Preferences.Get("hand", nameof(Handedness.Centre)), out var h) ? h : Handedness.Centre;
        set => Set("hand", value.ToString());
    }

    public Density Density
    {
        get => Enum.TryParse<Density>(Preferences.Get("density", nameof(Density.Compact)), out var d) ? d : Density.Compact;
        set => Set("density", value.ToString());
    }

    public bool ReduceTransparency
    {
        get => Preferences.Get("reduceTransparency", false);
        set => Set("reduceTransparency", value);
    }

    public double ReadingFontSize
    {
        get => Preferences.Get("readingFontSize", 17.0);
        set => Set("readingFontSize", Math.Clamp(value, 14, 28));
    }

    public bool AllowRemoteImages
    {
        get => Preferences.Get("allowRemoteImages", false);
        set => Set("allowRemoteImages", value);
    }

    public double RowHeight => Density switch
    {
        Density.Comfortable => Core.Theming.ThemeCatalog.Token("size", "h-row-comfortable"),
        Density.Dense => Core.Theming.ThemeCatalog.Token("size", "h-row-dense"),
        _ => Core.Theming.ThemeCatalog.Token("size", "h-row")
    };

    private void Set(string key, string value)
    {
        Preferences.Set(key, value);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(string key, bool value)
    {
        Preferences.Set(key, value);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(string key, double value)
    {
        Preferences.Set(key, value);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
