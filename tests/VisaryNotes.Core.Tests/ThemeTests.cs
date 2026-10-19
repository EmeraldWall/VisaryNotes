using VisaryNotes.Core.Theming;

namespace VisaryNotes.Core.Tests;

/// <summary>The same targets as tools/design/contrast.py, so the app cannot ship a theme that fails them.</summary>
public class ThemeTests
{
    public static TheoryData<string> ThemeNames()
    {
        var data = new TheoryData<string>();
        foreach (var t in ThemeCatalog.Themes)
            data.Add(t.Name);
        return data;
    }

    [Fact]
    public void Seven_themes_with_Daylight_as_default()
    {
        Assert.Equal(7, ThemeCatalog.Themes.Count);
        Assert.Equal("Daylight", ThemeCatalog.Get(null).Name);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void Text_meets_contrast_targets(string name)
    {
        var t = ThemeCatalog.Get(name);
        Assert.True(Contrast.Ratio(t.Ink, t.Paper) >= 7, "body text on paper");
        Assert.True(Contrast.Ratio(t.Soft, t.Paper) >= 4.5, "secondary text on paper");
        Assert.True(Contrast.Ratio(t.OnAccent, t.Accent) >= 4.5, "label on accent");
        Assert.True(Contrast.Ratio(t.AccentText, t.Paper) >= 4.5, "link text");
        Assert.True(Contrast.Ratio(t.Accent, t.Paper) >= 3, "accent as UI");
        Assert.True(Contrast.Ratio(t.Line, t.Paper) >= 3, "dividers");
        Assert.True(Contrast.Ratio(t.Ink, t.Highlight) >= 7, "text on highlight");
        Assert.True(Contrast.Ratio(t.Ink, t.Selection) >= 7, "text on selection");
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void Clear_glass_stays_legible_over_typical_content(string name)
    {
        var t = ThemeCatalog.Get(name);
        var glass = Contrast.Parse(t.Glass);
        var paper = Contrast.Parse(t.Paper);
        var ink = Contrast.Parse(t.Ink);
        var textAverage = Contrast.Over(ink, 0.15, paper);
        foreach (var back in new[] { paper, textAverage, Contrast.Parse(t.Highlight), Contrast.Parse(t.Desk) })
        {
            var surface = Contrast.Over(glass, t.GlassAlpha, back);
            Assert.True(Contrast.Ratio(ink, surface) >= 7);
            Assert.True(Contrast.Ratio(Contrast.Parse(t.Soft), surface) >= 4.5);
        }
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void Adaptive_glass_stays_legible_over_any_image(string name)
    {
        var t = ThemeCatalog.Get(name);
        var glass = Contrast.Parse(t.Glass);
        foreach (var back in new[] { new double[] { 1, 1, 1 }, new double[] { 0, 0, 0 } })
        {
            var surface = Contrast.Over(glass, t.GlassAlphaMax, back);
            Assert.True(Contrast.Ratio(Contrast.Parse(t.Ink), surface) >= 7);
            Assert.True(Contrast.Ratio(Contrast.Parse(t.Soft), surface) >= 4.5);
        }
    }

    [Fact]
    public void Tokens_are_readable()
    {
        Assert.Equal(16, ThemeCatalog.Token("space", "sp-4"));
        Assert.Equal(56, ThemeCatalog.Token("size", "h-primary"));
    }
}
