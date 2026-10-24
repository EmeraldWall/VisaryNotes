using System.Reflection;
using System.Text.Json;

namespace VisaryNotes.Core.Theming;

/// <summary>One colour theme from tools/design/themes.json.</summary>
public sealed class Theme
{
    public Theme(string name, IReadOnlyDictionary<string, string> colors, GlassTokens glass)
    {
        Name = name;
        Colors = colors;
        IsDark = Contrast.Luminance(Contrast.Parse(colors["paper"])) < 0.2;
        IsHighContrast = name.StartsWith("High Contrast", StringComparison.Ordinal);
        GlassAlpha = IsHighContrast ? 1.0 : IsDark ? glass.AlphaBaseDark : glass.AlphaBaseLight;
        GlassAlphaMax = IsHighContrast ? 1.0 : IsDark ? glass.AlphaMaxDark : glass.AlphaMaxLight;
        GlassRimExtra = IsHighContrast ? 0 : glass.AlphaRimExtra;
    }

    public string Name { get; }
    public IReadOnlyDictionary<string, string> Colors { get; }
    public bool IsDark { get; }
    public bool IsHighContrast { get; }
    public double GlassAlpha { get; }
    public double GlassAlphaMax { get; }
    public double GlassRimExtra { get; }

    public string this[string key] => Colors[key];

    public string Paper => Colors["paper"];
    public string Desk => Colors["desk"];
    public string Ink => Colors["ink"];
    public string Soft => Colors["soft"];
    public string Glass => Colors["glass"];
    public string Accent => Colors["accent"];
    public string AccentText => Colors["accentText"];
    public string OnAccent => Colors["onAccent"];
    public string Highlight => Colors["hl"];
    public string Selection => Colors["sel"];
    public string Line => Colors["line"];
}

public sealed record GlassTokens(double AlphaBaseLight, double AlphaBaseDark, double AlphaMaxLight, double AlphaMaxDark, double AlphaRimExtra);

/// <summary>Loads the embedded design tokens so the app uses exactly the values the design checks measure.</summary>
public static class ThemeCatalog
{
    private static readonly Lazy<IReadOnlyList<Theme>> All = new(Load);
    private static readonly Lazy<JsonDocument> TokenDoc = new(() => JsonDocument.Parse(ReadResource("VisaryNotes.tokens.json")));

    public static IReadOnlyList<Theme> Themes => All.Value;

    public const string DefaultTheme = "Daylight";

    public static Theme Get(string? name) =>
        Themes.FirstOrDefault(t => t.Name == name) ?? Themes.First(t => t.Name == DefaultTheme);

    /// <summary>A numeric token such as space.sp-4 or size.h-primary.</summary>
    public static double Token(string group, string key) =>
        TokenDoc.Value.RootElement.GetProperty(group).GetProperty(key).GetDouble();

    private static IReadOnlyList<Theme> Load()
    {
        var glass = TokenDoc.Value.RootElement.GetProperty("glass");
        var g = new GlassTokens(
            glass.GetProperty("alpha-base-light").GetDouble(),
            glass.GetProperty("alpha-base-dark").GetDouble(),
            glass.GetProperty("alpha-max-light").GetDouble(),
            glass.GetProperty("alpha-max-dark").GetDouble(),
            glass.GetProperty("alpha-rim-extra").GetDouble());

        using var doc = JsonDocument.Parse(ReadResource("VisaryNotes.themes.json"));
        var list = new List<Theme>();
        foreach (var t in doc.RootElement.EnumerateObject())
        {
            var colors = t.Value.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!);
            list.Add(new Theme(t.Name, colors, g));
        }
        return list;
    }

    private static string ReadResource(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

/// <summary>WCAG 2 contrast maths, matching tools/design/contrast.py.</summary>
public static class Contrast
{
    public static double[] Parse(string hex)
    {
        var h = hex.TrimStart('#');
        return
        [
            Convert.ToInt32(h[..2], 16) / 255.0,
            Convert.ToInt32(h[2..4], 16) / 255.0,
            Convert.ToInt32(h[4..6], 16) / 255.0
        ];
    }

    private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    public static double Luminance(double[] rgb) => 0.2126 * Linear(rgb[0]) + 0.7152 * Linear(rgb[1]) + 0.0722 * Linear(rgb[2]);

    public static double Ratio(double[] a, double[] b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    public static double Ratio(string a, string b) => Ratio(Parse(a), Parse(b));

    public static double[] Over(double[] fg, double alpha, double[] bg) =>
        [alpha * fg[0] + (1 - alpha) * bg[0], alpha * fg[1] + (1 - alpha) * bg[1], alpha * fg[2] + (1 - alpha) * bg[2]];
}
