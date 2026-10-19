using System.Text;
using VisaryNotes.Core.Documents;
using VisaryNotes.Core.Rendering;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.Core.Tests;

public class RenderingTests
{
    private static readonly ReaderSettings Settings = new(ThemeCatalog.Get("Daylight"));

    [Fact]
    public void Markdown_never_passes_raw_html_through()
    {
        var html = MarkdownRenderer.ToHtml("Hello <script>alert(1)</script> <img src=x onerror=alert(1)>");
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
    }

    [Fact]
    public void Markdown_blocks_remote_images_unless_allowed()
    {
        Assert.DoesNotContain("example.org", MarkdownRenderer.ToHtml("![a](https://example.org/a.png)"));
        Assert.Contains("example.org", MarkdownRenderer.ToHtml("![a](https://example.org/a.png)", allowRemoteImages: true));
    }

    [Fact]
    public void Markdown_supports_gfm_tables_and_task_lists()
    {
        var html = MarkdownRenderer.ToHtml("| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n- [ ] todo");
        Assert.Contains("<table>", html);
        Assert.Contains("type=\"checkbox\"", html);
    }

    [Fact]
    public void Note_title_comes_from_first_heading()
    {
        Assert.Equal("Garden plan", MarkdownRenderer.TitleOf("\n# Garden plan\n\nText"));
    }

    [Fact]
    public void Reading_view_escapes_text_and_uses_theme_colours()
    {
        var doc = new Document { Title = "T" };
        var p = new Paragraph();
        p.Runs.Add(new Run("<b>not bold</b>", TextStyle.Plain));
        doc.Blocks.Add(p);
        var html = HtmlRenderer.Render(doc, ViewMode.Reading, Settings);
        Assert.Contains("&lt;b&gt;not bold&lt;/b&gt;", html);
        Assert.Contains("--paper:#FBFAF7", html);
    }

    [Fact]
    public void Original_view_keeps_page_size_and_reports_font_substitution()
    {
        var rtf = @"{\rtf1{\fonttbl{\f0 Times New Roman;}}\paperw12240\paperh15840\f0 Hello\par}";
        var doc = RtfReader.Read(new MemoryStream(Encoding.ASCII.GetBytes(rtf)), "a.rtf");
        var html = HtmlRenderer.Render(doc, ViewMode.Original, Settings);
        Assert.Contains("width:215.9mm", html);
        Assert.Contains("Times New Roman shown as Tinos", html);
    }

    [Fact]
    public void Highlights_are_passed_to_the_page_safely()
    {
        var doc = new Document();
        var html = HtmlRenderer.Render(doc, ViewMode.Reading, Settings, ["</script><script>alert(1)"]);
        Assert.DoesNotContain("</script><script>alert(1)", html);
    }

    [Fact]
    public void Plain_text_reading_view_reflows_but_original_keeps_layout()
    {
        var doc = PlainTextReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("col1   col2\nnext line\n\nsecond para")), "a.txt");
        Assert.Contains("<pre class=\"original-text\">", HtmlRenderer.Render(doc, ViewMode.Original, Settings));
        Assert.Contains("<p class=\"plain\">second para</p>", HtmlRenderer.Render(doc, ViewMode.Reading, Settings));
    }

    [Fact]
    public void Loader_picks_reader_by_extension()
    {
        Assert.Equal(DocumentKind.Rtf, DocumentLoader.KindOf("a.RTF"));
        Assert.Equal(DocumentKind.Odt, DocumentLoader.KindOf("a.fodt"));
        Assert.Equal(DocumentKind.Markdown, DocumentLoader.KindOf("notes", "text/markdown"));
        Assert.Throws<DocumentReadException>(() => DocumentLoader.Load(new MemoryStream(), "a.xyz"));
    }
}
