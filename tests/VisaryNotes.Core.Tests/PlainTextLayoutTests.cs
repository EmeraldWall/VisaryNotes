using VisaryNotes.Core.Rendering;

namespace VisaryNotes.Core.Tests;

public class PlainTextLayoutTests
{
    private const string Licence = """
        This Font Software is licensed under the SIL Open Font License, Version 1.1.
        This license is copied below, and is also available with a FAQ at:
        https://openfontlicense.org

        -----------------------------------------------------------
        SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
        -----------------------------------------------------------

        PREAMBLE
        The goals of the Open Font License (OFL) are to stimulate worldwide
        development of collaborative font projects, to support the font creation
        efforts of academic and linguistic communities, and to provide a free and
        open framework in which fonts may be shared and improved in partnership
        with others.
        """;

    [Fact]
    public void Reading_joins_hard_wrapped_lines_into_one_paragraph()
    {
        var html = PlainTextLayout.Reading(Licence);
        Assert.Contains("stimulate worldwide development of collaborative font projects, to support the font creation efforts", html);
        Assert.DoesNotContain("worldwide<br>", html);
    }

    [Fact]
    public void Reading_keeps_short_lines_and_lines_ending_in_a_colon()
    {
        var html = PlainTextLayout.Reading("Jane Doe\n12 High Street\nLeeds\n\nItems we need for the trip are listed here:\n- boots\n- maps and a compass for the long walk on day two\n");
        Assert.Contains("Jane Doe<br>12 High Street<br>Leeds", html);
        Assert.Contains("listed here:<br>- boots<br>- maps", html);
    }

    [Fact]
    public void Separator_lines_become_rules_and_never_leave_a_stray_dash()
    {
        var html = PlainTextLayout.Reading(Licence);
        Assert.Equal(2, html.Split("<hr>").Length - 1);
        Assert.DoesNotContain("---", html);
        Assert.Contains("<p class=\"plain\">SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007</p>", html);
        Assert.True(PlainTextLayout.IsRule("= = = = ="));
        Assert.False(PlainTextLayout.IsRule("- item"));
    }

    [Fact]
    public void Original_keeps_text_as_written_and_clips_separator_lines()
    {
        var html = PlainTextLayout.Original("a\n-----\nb");
        Assert.Equal("<pre class=\"original-text\">a\n<span class=\"rule-line\">-----</span>b</pre>", html);
    }
}
