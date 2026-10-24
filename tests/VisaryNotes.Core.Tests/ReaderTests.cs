using System.IO.Compression;
using System.Text;
using VisaryNotes.Core.Documents;

namespace VisaryNotes.Core.Tests;

public class PlainTextReaderTests
{
    [Fact]
    public void Utf8_with_bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("café")).ToArray();
        Assert.Equal("café", PlainTextReader.Decode(bytes).Text);
    }

    [Fact]
    public void Falls_back_to_windows_1252_for_invalid_utf8()
    {
        var bytes = new byte[] { 0x63, 0x61, 0x66, 0xE9 }; // "caf" + e acute in Windows 1252
        var (text, enc) = PlainTextReader.Decode(bytes);
        Assert.Equal("café", text);
        Assert.Equal(1252, enc.CodePage);
    }

    [Theory]
    [InlineData("a\r\nb\r\n", "CRLF")]
    [InlineData("a\nb\n", "LF")]
    [InlineData("a\rb\r", "CR")]
    public void Detects_line_endings(string text, string expected) =>
        Assert.Equal(expected, PlainTextReader.DetectLineEnding(text));

    [Fact]
    public void Keeps_text_as_preformatted_block()
    {
        var doc = PlainTextReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("line 1\r\n  indented")), "log.txt");
        var pre = Assert.IsType<Preformatted>(Assert.Single(doc.Blocks));
        Assert.Equal("line 1\n  indented", pre.Text);
    }
}

public class RtfReaderTests
{
    private static Document Read(string rtf) =>
        RtfReader.Read(new MemoryStream(Encoding.ASCII.GetBytes(rtf)), "test.rtf");

    [Fact]
    public void Rejects_files_that_are_not_rtf() =>
        Assert.Throws<DocumentReadException>(() => Read("hello"));

    [Fact]
    public void Reads_character_formatting_fonts_and_colours()
    {
        var doc = Read(@"{\rtf1\ansi\deff0{\fonttbl{\f0\froman Times New Roman;}{\f1\fswiss Arial;}}{\colortbl;\red255\green0\blue0;}
\pard\qc\f1\fs28 Plain {\b bold} {\i\cf1 red italic}\par}");
        var p = Assert.IsType<Paragraph>(doc.Blocks[0]);
        Assert.Equal(TextAlign.Center, p.Style.Align);
        Assert.Equal("Plain bold red italic", p.Text);
        var bold = p.Runs.Single(r => r.Text == "bold");
        Assert.True(bold.Style.Bold);
        Assert.Equal("Arial", bold.Style.FontFamily);
        Assert.Equal(14, bold.Style.FontSizePt);
        var red = p.Runs.Single(r => r.Text == "red italic");
        Assert.True(red.Style.Italic);
        Assert.Equal("#FF0000", red.Style.Color);
    }

    [Fact]
    public void Reads_unicode_and_code_page_characters()
    {
        // Built from parts so C# never sees a backslash u sequence it could turn into a character itself.
        var doc = Read(@"{\rtf1\ansi\ansicpg1252\uc1 caf\'e9 \" + "u8364?" + @"\par}");
        Assert.Equal("café €", ((Paragraph)doc.Blocks[0]).Text);
    }

    [Fact]
    public void Skips_unknown_and_metadata_destinations_but_keeps_title()
    {
        var doc = Read(@"{\rtf1{\info{\title Lease renewal}{\author Pat}}{\*\unknowndest secret}{\stylesheet{\s0 Normal;}}Visible\par}");
        Assert.Equal("Lease renewal", doc.Title);
        Assert.Equal("Pat", doc.Author);
        Assert.Equal("Visible", ((Paragraph)doc.Blocks[0]).Text);
    }

    [Fact]
    public void Reads_tables()
    {
        var doc = Read(@"{\rtf1\trowd\cellx2000\cellx4000\intbl Week\cell Volunteer\cell\row\trowd\cellx2000\cellx4000\intbl 1\cell Amara\cell\row\pard After\par}");
        var table = Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Amara", ((Paragraph)table.Rows[1][1].Blocks[0]).Text);
        Assert.Equal("After", ((Paragraph)doc.Blocks[1]).Text);
    }

    [Fact]
    public void Reads_hyperlinks()
    {
        var doc = Read(@"{\rtf1 See {\field{\*\fldinst{HYPERLINK ""https://example.org/""}}{\fldrslt{the site}}}.\par}");
        var p = (Paragraph)doc.Blocks[0];
        Assert.Equal("See the site.", p.Text);
        Assert.Equal("https://example.org/", p.Runs.Single(r => r.Text == "the site").Link);
    }

    [Fact]
    public void Ignores_unsafe_hyperlink_schemes()
    {
        var doc = Read(@"{\rtf1 {\field{\*\fldinst{HYPERLINK ""javascript:alert(1)""}}{\fldrslt{click}}}\par}");
        Assert.Null(((Paragraph)doc.Blocks[0]).Runs.Single().Link);
    }

    [Fact]
    public void Reads_png_pictures_and_page_size()
    {
        var png = "89504E470D0A1A0A0000000D49484452";
        var doc = Read(@"{\rtf1\paperw11906\paperh16838\margl1440{\pict\pngblip " + png + @"}\par}");
        var img = Assert.IsType<ImageBlock>(doc.Blocks[0]);
        Assert.Equal("image/png", img.MimeType);
        Assert.Equal(210, doc.Page.WidthMm, 0);
        Assert.Equal(297, doc.Page.HeightMm, 0);
        Assert.Equal(25.4, doc.Page.MarginLeftMm, 1);
    }

    [Fact]
    public void Survives_deep_nesting_without_hanging()
    {
        var rtf = @"{\rtf1" + new string('{', 300) + "x" + new string('}', 300) + "}";
        Assert.Throws<DocumentReadException>(() => Read(rtf));
    }
}

public class OdtReaderTests
{
    private const string ContentNs =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
        "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
        "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
        "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
        "xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\"";

    private static readonly byte[] TinyPng = Convert.FromHexString("89504E470D0A1A0A0000000D4948445200000001000000010806000000");

    private static byte[] BuildOdt(string body, string automaticStyles = "", string? extraEntry = null)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open());
                w.Write(text);
            }
            Add("mimetype", "application/vnd.oasis.opendocument.text");
            Add("content.xml", $"<office:document-content {ContentNs}><office:automatic-styles>{automaticStyles}</office:automatic-styles><office:body><office:text>{body}</office:text></office:body></office:document-content>");
            Add("styles.xml", $"<office:document-styles {ContentNs}><office:styles><style:style style:name=\"Heading\" style:family=\"paragraph\"><style:text-properties fo:font-weight=\"bold\"/></style:style></office:styles>" +
                "<office:automatic-styles><style:page-layout style:name=\"pm1\"><style:page-layout-properties fo:page-width=\"21.59cm\" fo:page-height=\"27.94cm\" fo:margin-top=\"2cm\" fo:margin-bottom=\"2cm\" fo:margin-left=\"2.5cm\" fo:margin-right=\"2.5cm\"/></style:page-layout></office:automatic-styles>" +
                "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\"><style:header><text:p>Riverside Community Garden</text:p></style:header></style:master-page></office:master-styles></office:document-styles>");
            Add("meta.xml", $"<office:document-meta {ContentNs}><office:meta><dc:title>Autumn Newsletter</dc:title><dc:creator>Amara</dc:creator></office:meta></office:document-meta>");
            using (var pic = zip.CreateEntry("Pictures/a.png").Open())
                pic.Write(TinyPng);
            if (extraEntry is not null)
                Add(extraEntry, "x");
        }
        return ms.ToArray();
    }

    private static Document Read(byte[] bytes) => OdtReader.Read(new MemoryStream(bytes), "test.odt");

    [Fact]
    public void Reads_metadata_page_layout_and_header()
    {
        var doc = Read(BuildOdt("<text:p>Hello</text:p>"));
        Assert.Equal("Autumn Newsletter", doc.Title);
        Assert.Equal("Amara", doc.Author);
        Assert.Equal(215.9, doc.Page.WidthMm, 1);
        Assert.Equal(25, doc.Page.MarginLeftMm, 1);
        Assert.Equal("Riverside Community Garden", doc.HeaderText);
    }

    [Fact]
    public void Reads_headings_styles_spacing_and_links()
    {
        var doc = Read(BuildOdt(
            "<text:h text:outline-level=\"2\">Harvest day</text:h>" +
            "<text:p text:style-name=\"P1\">Bring<text:s text:c=\"2\"/><text:span text:style-name=\"T1\">gloves</text:span> <text:a xlink:href=\"https://example.org\">here</text:a></text:p>",
            "<style:style style:name=\"P1\" style:family=\"paragraph\"><style:paragraph-properties fo:text-align=\"center\"/></style:style>" +
            "<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\" fo:color=\"#336699\"/></style:style>"));
        var h = Assert.IsType<Heading>(doc.Blocks[0]);
        Assert.Equal(2, h.Level);
        Assert.Equal("Harvest day", h.Text);
        var p = Assert.IsType<Paragraph>(doc.Blocks[1]);
        Assert.Equal(TextAlign.Center, p.Style.Align);
        Assert.Equal("Bring  gloves here", p.Text);
        var gloves = p.Runs.Single(r => r.Text == "gloves");
        Assert.True(gloves.Style.Bold);
        Assert.Equal("#336699", gloves.Style.Color);
        Assert.Equal("https://example.org", p.Runs.Single(r => r.Text == "here").Link);
    }

    [Fact]
    public void Reads_lists_tables_and_images()
    {
        var doc = Read(BuildOdt(
            "<text:list text:style-name=\"L1\"><text:list-item><text:p>First</text:p></text:list-item><text:list-item><text:p>Second</text:p></text:list-item></text:list>" +
            "<table:table><table:table-row><table:table-cell table:number-columns-spanned=\"2\"><text:p>Week</text:p></table:table-cell></table:table-row></table:table>" +
            "<text:p><draw:frame svg:width=\"5cm\"><draw:image xlink:href=\"Pictures/a.png\"/></draw:frame></text:p>",
            "<text:list-style style:name=\"L1\"><text:list-level-style-number text:level=\"1\"/></text:list-style>"));
        var list = Assert.IsType<ListBlock>(doc.Blocks[0]);
        Assert.True(list.Ordered);
        Assert.Equal(2, list.Items.Count);
        var table = Assert.IsType<TableBlock>(doc.Blocks[1]);
        Assert.Equal(2, table.Rows[0][0].ColumnSpan);
        var img = Assert.IsType<ImageBlock>(doc.Blocks[2]);
        Assert.Equal("image/png", img.MimeType);
        Assert.Equal(50, img.WidthMm);
    }

    [Fact]
    public void Rejects_path_traversal_in_the_package() =>
        Assert.Throws<DocumentReadException>(() => Read(BuildOdt("<text:p>x</text:p>", extraEntry: "../evil.txt")));

    [Fact]
    public void Rejects_xml_with_a_dtd()
    {
        var flat = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><office:document " + ContentNs + "><office:body><office:text><text:p>&e;</text:p></office:text></office:body></office:document>";
        Assert.ThrowsAny<Exception>(() => OdtReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(flat)), "x.fodt"));
    }

    [Fact]
    public void Reads_flat_odt()
    {
        var flat = $"<office:document {ContentNs}><office:meta><dc:title>Flat</dc:title></office:meta><office:body><office:text><text:p>Hello flat</text:p></office:text></office:body></office:document>";
        var doc = OdtReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(flat)), "x.fodt");
        Assert.Equal("Flat", doc.Title);
        Assert.Equal("Hello flat", ((Paragraph)doc.Blocks[0]).Text);
    }

    [Theory]
    [InlineData("2cm", 20)]
    [InlineData("1in", 25.4)]
    [InlineData("72pt", 25.4)]
    [InlineData("10mm", 10)]
    public void Parses_lengths(string value, double mm) => Assert.Equal(mm, OdtReader.ParseLengthMm(value)!.Value, 3);
}
