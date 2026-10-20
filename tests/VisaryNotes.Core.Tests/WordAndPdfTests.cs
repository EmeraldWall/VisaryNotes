using System.IO.Compression;
using System.Text;
using VisaryNotes.Core.Documents;
using VisaryNotes.Core.Rendering;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.Core.Tests;

internal static class Fixture
{
    public static LoadedDocument Load(string name)
    {
        using var s = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        return DocumentLoader.Load(s, name);
    }
}

public class DocxReaderTests
{
    private readonly Document _doc = Fixture.Load("sample.docx").Model!;

    [Fact]
    public void Reads_title_and_page_size()
    {
        Assert.Equal("Visary Sample", _doc.Title);
        Assert.Equal(210, _doc.Page.WidthMm, 0);
        Assert.Equal(297, _doc.Page.HeightMm, 0);
    }

    [Fact]
    public void Reads_headings_and_character_formatting()
    {
        var headings = _doc.Blocks.OfType<Heading>().ToList();
        Assert.Equal(("Quarterly Reading Notes", 1), (headings[0].Text, headings[0].Level));
        Assert.Equal(("Highlights", 2), (headings[1].Text, headings[1].Level));

        var p = _doc.Blocks.OfType<Paragraph>().First();
        Assert.Contains(p.Runs, r => r.Text == "bold words" && r.Style.Bold);
        Assert.Contains(p.Runs, r => r.Text == "italic words" && r.Style.Italic);
        Assert.Contains(p.Runs, r => r.Text == "underlined words" && r.Style.Underline);
        Assert.Contains(p.Runs, r => r.Text == "an example site" && r.Link == "https://example.com/");
    }

    [Fact]
    public void Reads_lists_table_and_image()
    {
        var lists = _doc.Blocks.OfType<ListBlock>().ToList();
        Assert.Equal(2, lists.Count);
        Assert.False(lists[0].Ordered);
        Assert.True(lists[1].Ordered);
        Assert.Equal("Step two", lists[1].Items[1].OfType<Paragraph>().Single().Text);

        var table = _doc.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("Alice", table.Rows[1][0].Blocks.OfType<Paragraph>().Single().Text);

        var image = _doc.Blocks.OfType<ImageBlock>().Single();
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal("Chart", image.Alt);
        Assert.Contains("café", _doc.PlainText());
    }

    [Fact]
    public void Merges_vertical_cells_and_skips_deleted_text_and_field_codes()
    {
        const string body = """
            <w:tbl>
              <w:tr><w:tc><w:tcPr><w:vMerge w:val="restart"/></w:tcPr><w:p><w:r><w:t>Tall</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>A</w:t></w:r></w:p></w:tc></w:tr>
              <w:tr><w:tc><w:tcPr><w:vMerge/></w:tcPr><w:p/></w:tc><w:tc><w:p><w:r><w:t>B</w:t></w:r></w:p></w:tc></w:tr>
            </w:tbl>
            <w:p><w:r><w:t xml:space="preserve">Kept </w:t></w:r><w:del><w:r><w:delText>gone</w:delText></w:r></w:del><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText>PAGE</w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>7</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
            """;
        var doc = DocxReader.Read(new MemoryStream(MinimalDocx(body)), "t.docx");
        var table = doc.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(2, table.Rows[0][0].RowSpan);
        Assert.Single(table.Rows[1]);
        Assert.Equal("Kept 7", doc.Blocks.OfType<Paragraph>().Single().Text);
    }

    [Fact]
    public void Rejects_a_doc_renamed_to_docx_with_a_clear_message()
    {
        var ex = Assert.Throws<DocumentReadException>(() => DocxReader.Read(new MemoryStream(Encoding.ASCII.GetBytes("not a zip at all")), "x.docx"));
        Assert.Contains(".docx", ex.Message);
    }

    [Fact]
    public void Rejects_path_traversal_in_the_package()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            zip.CreateEntry("../evil.xml");
        Assert.Throws<DocumentReadException>(() => DocxReader.Read(new MemoryStream(ms.ToArray()), "x.docx"));
    }

    internal static byte[] MinimalDocx(string bodyXml)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open());
                w.Write(text);
            }
            Add("word/document.xml", $"""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{bodyXml}</w:body></w:document>""");
        }
        return ms.ToArray();
    }
}

public class DocReaderTests
{
    private readonly Document _doc = Fixture.Load("sample.doc").Model!;

    [Fact]
    public void Reads_text_headings_and_formatting()
    {
        var headings = _doc.Blocks.OfType<Heading>().ToList();
        Assert.Equal(("Quarterly Reading Notes", 1), (headings[0].Text, headings[0].Level));
        Assert.Equal(("Highlights", 2), (headings[1].Text, headings[1].Level));
        var p = _doc.Blocks.OfType<Paragraph>().First();
        Assert.Contains(p.Runs, r => r.Text.Contains("bold words") && r.Style.Bold);
        Assert.Contains(p.Runs, r => r.Text.Contains("italic words") && r.Style.Italic);
        Assert.Contains("café", _doc.PlainText());
    }

    [Fact]
    public void Reads_tables_and_drops_field_codes()
    {
        var table = _doc.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["Bob", "78"], table.Rows[2].Select(c => c.Blocks.OfType<Paragraph>().Single().Text));
        Assert.DoesNotContain("HYPERLINK", _doc.PlainText());
        Assert.Contains("an example site", _doc.PlainText());
    }

    [Fact]
    public void Rejects_files_that_are_not_compound_files() =>
        Assert.Throws<DocumentReadException>(() => DocReader.Read(new MemoryStream(new byte[600]), "x.doc"));
}

public class PdfReaderTests
{
    private readonly LoadedDocument _loaded = Fixture.Load("sample.pdf");

    [Fact]
    public void Reflows_text_into_headings_paragraphs_and_lists()
    {
        var doc = _loaded.Model!;
        Assert.Equal("Visary Sample", doc.Title);
        Assert.Equal("Quarterly Reading Notes", doc.Blocks.OfType<Heading>().First().Text);
        Assert.Contains(doc.Blocks.OfType<Paragraph>(), p => p.Text.StartsWith("This paragraph has bold words"));
        var lists = doc.Blocks.OfType<ListBlock>().ToList();
        Assert.Contains(lists, l => !l.Ordered && l.Items.Count == 2);
        Assert.Contains(lists, l => l.Ordered && l.Items[0].OfType<Paragraph>().Single().Text == "Step one");
        Assert.Contains("café", doc.PlainText());
    }

    [Fact]
    public void Keeps_page_sizes_for_original_view()
    {
        var page = Assert.Single(_loaded.Model!.FixedPages);
        Assert.Equal(210, page.WidthMm, 0);
        Assert.Equal(297, page.HeightMm, 0);
    }

    [Fact]
    public void Original_view_lays_out_one_placeholder_per_page()
    {
        var html = _loaded.Html(ViewMode.Original, new ReaderSettings(ThemeCatalog.Get("Daylight")));
        Assert.Contains("data-pdf-page=\"1\"", html);
        Assert.DoesNotContain("data-pdf-page=\"2\"", html);
    }

    [Theory]
    [InlineData(new[] { "An exam-", "ple line" }, "An example line")]
    [InlineData(new[] { "Well-", "Known" }, "Well- Known")]
    [InlineData(new[] { "one", "two" }, "one two")]
    public void Joins_lines_and_hyphenated_words(string[] lines, string expected) =>
        Assert.Equal(expected, PdfReader.JoinLines(lines));

    [Theory]
    [InlineData("• Point", "•")]
    [InlineData("12. Twelve", "12.")]
    [InlineData("b) Bee", "b)")]
    [InlineData("2026 was a year", null)]
    public void Finds_list_markers(string line, string? marker) => Assert.Equal(marker, PdfReader.ListMarker(line));

    [Fact]
    public void Rejects_files_that_are_not_pdf() =>
        Assert.Throws<DocumentReadException>(() => PdfReader.Read(new MemoryStream(Encoding.ASCII.GetBytes("hello")), "x.pdf"));
}

public class SniffTests
{
    [Fact]
    public void A_docx_saved_as_doc_opens_as_docx()
    {
        using var s = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.docx"));
        Assert.Equal(DocumentKind.Docx, DocumentLoader.Load(s, "renamed.doc").Kind);
    }

    [Fact]
    public void Rtf_saved_as_doc_opens_as_rtf() =>
        Assert.Equal(DocumentKind.Rtf, DocumentLoader.Load(new MemoryStream(Encoding.ASCII.GetBytes(@"{\rtf1 Hello}")), "letter.doc").Kind);

    [Fact]
    public void Text_files_keep_their_kind() =>
        Assert.Equal(DocumentKind.PlainText, DocumentLoader.Sniff(Encoding.ASCII.GetBytes("PK is just text here"), DocumentKind.PlainText));
}
