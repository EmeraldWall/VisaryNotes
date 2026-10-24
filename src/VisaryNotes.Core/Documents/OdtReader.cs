using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace VisaryNotes.Core.Documents;

/// <summary>
/// Reads OpenDocument Text (.odt) and flat ODT (.fodt) into the shared document model
/// (docs/SPEC.md 7.5, OASIS OpenDocument 1.3).
/// </summary>
public sealed class OdtReader
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace StyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace TableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

    private sealed record StyleDef(string Name, string Family, string? Parent, XElement? TextProps, XElement? ParaProps);

    private readonly Dictionary<string, StyleDef> _styles = new();
    private readonly Dictionary<string, StyleDef> _defaults = new();
    private readonly Dictionary<string, string> _fontFaces = new();
    private readonly HashSet<string> _orderedLists = new();
    private readonly Func<string, byte[]?> _loadPicture;
    private readonly Document _doc = new() { Format = DocumentFormat.Odt };

    private OdtReader(Func<string, byte[]?> loadPicture) => _loadPicture = loadPicture;

    public static Document Read(Stream stream, string fileName)
    {
        var bytes = ReadLimits.ReadAll(stream);
        Document doc;
        if (bytes.Length > 4 && bytes[0] == 'P' && bytes[1] == 'K')
            doc = ReadPackage(bytes);
        else
            doc = ReadFlat(bytes);
        if (string.IsNullOrWhiteSpace(doc.Title))
            doc.Title = Path.GetFileNameWithoutExtension(fileName);
        return doc;
    }

    public static XmlReaderSettings SafeXml() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = ReadLimits.MaxUncompressedBytes,
        IgnoreComments = true
    };

    private static XDocument LoadXml(Stream s)
    {
        using var reader = XmlReader.Create(s, SafeXml());
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static Document ReadPackage(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count > ReadLimits.MaxZipEntries)
            throw new DocumentReadException("The ODT package has too many entries.");
        long total = 0;
        foreach (var e in zip.Entries)
        {
            if (e.FullName.Contains("..") || e.FullName.StartsWith('/'))
                throw new DocumentReadException("The ODT package contains an unsafe path.");
            total += e.Length;
            if (total > ReadLimits.MaxUncompressedBytes)
                throw new DocumentReadException("The ODT package expands to more than the allowed size.");
        }

        XDocument? Entry(string name)
        {
            var e = zip.GetEntry(name);
            if (e is null)
                return null;
            using var s = e.Open();
            return LoadXml(s);
        }

        var content = Entry("content.xml") ?? throw new DocumentReadException("This file has no content.xml, so it is not a valid ODT document.");
        var styles = Entry("styles.xml");
        var meta = Entry("meta.xml");

        var reader = new OdtReader(href =>
        {
            var e = zip.GetEntry(href.TrimStart('.', '/'));
            if (e is null || e.Length > ReadLimits.MaxFileBytes)
                return null;
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        });
        return reader.Build(content.Root!, styles?.Root, meta?.Root);
    }

    private static Document ReadFlat(byte[] bytes)
    {
        XDocument xml;
        try
        {
            xml = LoadXml(new MemoryStream(bytes));
        }
        catch (XmlException ex)
        {
            throw new DocumentReadException("This file is not a valid ODT or flat ODT document.", ex);
        }
        var root = xml.Root!;
        if (root.Name != Office + "document")
            throw new DocumentReadException("This file is not a flat ODT document.");
        var reader = new OdtReader(_ => null);
        return reader.Build(root, root, root);
    }

    private Document Build(XElement content, XElement? styles, XElement? meta)
    {
        foreach (var root in new[] { styles, content })
        {
            if (root is null)
                continue;
            foreach (var ff in root.Descendants(StyleNs + "font-face"))
            {
                var name = (string?)ff.Attribute(StyleNs + "name");
                var family = ((string?)ff.Attribute(Svg + "font-family"))?.Trim('\'', '"');
                if (name is not null && family is not null)
                    _fontFaces[name] = family;
            }
            foreach (var s in root.Descendants(StyleNs + "default-style"))
            {
                var family = (string?)s.Attribute(StyleNs + "family") ?? "";
                _defaults[family] = new StyleDef("", family, null, s.Element(StyleNs + "text-properties"), s.Element(StyleNs + "paragraph-properties"));
            }
            foreach (var s in root.Descendants(StyleNs + "style"))
            {
                var name = (string?)s.Attribute(StyleNs + "name");
                if (name is null)
                    continue;
                _styles[name] = new StyleDef(name, (string?)s.Attribute(StyleNs + "family") ?? "",
                    (string?)s.Attribute(StyleNs + "parent-style-name"),
                    s.Element(StyleNs + "text-properties"), s.Element(StyleNs + "paragraph-properties"));
            }
            foreach (var ls in root.Descendants(TextNs + "list-style"))
            {
                var first = ls.Elements().FirstOrDefault();
                var name = (string?)ls.Attribute(StyleNs + "name");
                if (name is not null && first?.Name == TextNs + "list-level-style-number")
                    _orderedLists.Add(name);
            }
        }

        ReadPageLayout(styles ?? content);

        var metaRoot = meta?.Descendants(Office + "meta").FirstOrDefault();
        _doc.Title = metaRoot?.Element(Dc + "title")?.Value.Trim() ?? "";
        _doc.Author = metaRoot?.Element(Dc + "creator")?.Value.Trim() ?? metaRoot?.Element(Meta + "initial-creator")?.Value.Trim();

        var body = content.Descendants(Office + "text").FirstOrDefault()
            ?? throw new DocumentReadException("This document has no text body.");
        ReadBlocks(body, _doc.Blocks, 0);
        return _doc;
    }

    private void ReadPageLayout(XElement root)
    {
        var master = root.Descendants(StyleNs + "master-page").FirstOrDefault();
        var layoutName = (string?)master?.Attribute(StyleNs + "page-layout-name");
        var layout = root.Descendants(StyleNs + "page-layout")
            .FirstOrDefault(l => (string?)l.Attribute(StyleNs + "name") == layoutName)
            ?? root.Descendants(StyleNs + "page-layout").FirstOrDefault();
        var props = layout?.Element(StyleNs + "page-layout-properties");
        if (props is not null)
        {
            double L(string a, double fallback) => ParseLengthMm((string?)props.Attribute(Fo + a)) ?? fallback;
            var cols = (int?)props.Element(StyleNs + "columns")?.Attribute(Fo + "column-count") ?? 1;
            _doc.Page = new PageSetup(L("page-width", 210), L("page-height", 297), L("margin-top", 20), L("margin-right", 20),
                L("margin-bottom", 20), L("margin-left", 20), Math.Max(1, cols));
        }

        if (master is not null)
        {
            var header = master.Element(StyleNs + "header")?.Value.Trim();
            var footer = master.Element(StyleNs + "footer")?.Value.Trim();
            _doc.HeaderText = string.IsNullOrEmpty(header) ? null : header;
            _doc.FooterText = string.IsNullOrEmpty(footer) ? null : footer;
        }
    }

    private void ReadBlocks(XElement parent, List<Block> into, int depth)
    {
        if (depth > ReadLimits.MaxNestingDepth)
            throw new DocumentReadException("The document is nested too deeply.");

        foreach (var el in parent.Elements())
        {
            if (el.Name == TextNs + "p")
            {
                AddParagraph(el, into, null);
            }
            else if (el.Name == TextNs + "h")
            {
                var level = (int?)el.Attribute(TextNs + "outline-level") ?? 1;
                AddParagraph(el, into, level);
            }
            else if (el.Name == TextNs + "list")
            {
                into.Add(ReadList(el, depth));
            }
            else if (el.Name == TableNs + "table")
            {
                into.Add(ReadTable(el, depth));
            }
            else if (el.Name == TextNs + "section" || el.Name == TextNs + "index-body" || el.Name.LocalName.EndsWith("-index"))
            {
                ReadBlocks(el, into, depth + 1);
            }
        }
    }

    private ListBlock ReadList(XElement list, int depth)
    {
        var styleName = (string?)list.Attribute(TextNs + "style-name");
        var block = new ListBlock { Ordered = styleName is not null && _orderedLists.Contains(styleName) };
        foreach (var item in list.Elements().Where(e => e.Name == TextNs + "list-item" || e.Name == TextNs + "list-header"))
        {
            var blocks = new List<Block>();
            ReadBlocks(item, blocks, depth + 1);
            block.Items.Add(blocks);
        }
        return block;
    }

    private TableBlock ReadTable(XElement table, int depth)
    {
        var block = new TableBlock();
        var headerRows = table.Elements(TableNs + "table-header-rows").Elements(TableNs + "table-row").ToHashSet();
        // Rows in document order, including header rows and row groups, but not rows of nested tables.
        var ordered = table.Descendants(TableNs + "table-row")
            .Where(r => r.Ancestors(TableNs + "table").First() == table)
            .ToList();

        foreach (var row in ordered)
        {
            var cells = new List<TableCell>();
            foreach (var c in row.Elements(TableNs + "table-cell"))
            {
                var cell = new TableCell
                {
                    ColumnSpan = (int?)c.Attribute(TableNs + "number-columns-spanned") ?? 1,
                    RowSpan = (int?)c.Attribute(TableNs + "number-rows-spanned") ?? 1,
                    IsHeader = headerRows.Contains(row)
                };
                ReadBlocks(c, cell.Blocks, depth + 1);
                var repeat = Math.Min((int?)c.Attribute(TableNs + "number-columns-repeated") ?? 1, 64);
                for (var i = 0; i < repeat; i++)
                    cells.Add(i == 0 ? cell : CopyCell(cell));
            }
            if (cells.Count > 0)
                block.Rows.Add(cells);
        }
        return block;
    }

    private static TableCell CopyCell(TableCell c)
    {
        var copy = new TableCell { ColumnSpan = c.ColumnSpan, RowSpan = c.RowSpan, IsHeader = c.IsHeader };
        copy.Blocks.AddRange(c.Blocks);
        return copy;
    }

    private void AddParagraph(XElement el, List<Block> into, int? headingLevel)
    {
        var styleName = (string?)el.Attribute(TextNs + "style-name");
        var baseText = ResolveText(styleName, "paragraph");
        var para = ResolvePara(styleName);
        var runs = new List<Run>();
        var images = new List<ImageBlock>();
        ReadInline(el, baseText, null, runs, images);

        if (headingLevel.HasValue)
        {
            var h = new Heading(headingLevel.Value) { Style = para };
            h.Runs.AddRange(runs);
            into.Add(h);
        }
        else
        {
            var p = new Paragraph { Style = para };
            p.Runs.AddRange(runs);
            if (runs.Count > 0 || images.Count == 0)
                into.Add(p);
        }
        into.AddRange(images);
    }

    private void ReadInline(XElement el, TextStyle style, string? link, List<Run> runs, List<ImageBlock> images)
    {
        foreach (var node in el.Nodes())
        {
            switch (node)
            {
                case XText t:
                    var text = CollapseSpaces(t.Value);
                    if (text.Length > 0)
                        runs.Add(new Run(text, style, link));
                    break;
                case XElement e when e.Name == TextNs + "span":
                    ReadInline(e, Merge(style, ResolveText((string?)e.Attribute(TextNs + "style-name"), "text")), link, runs, images);
                    break;
                case XElement e when e.Name == TextNs + "a":
                    var href = (string?)e.Attribute(XLink + "href");
                    var safe = href is not null && Uri.TryCreate(href, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto" ? href : null;
                    ReadInline(e, Merge(style, ResolveText((string?)e.Attribute(TextNs + "style-name"), "text")), safe, runs, images);
                    break;
                case XElement e when e.Name == TextNs + "s":
                    var count = Math.Min((int?)e.Attribute(TextNs + "c") ?? 1, 1000);
                    runs.Add(new Run(new string(' ', count), style, link));
                    break;
                case XElement e when e.Name == TextNs + "tab":
                    runs.Add(new Run("\t", style, link));
                    break;
                case XElement e when e.Name == TextNs + "line-break":
                    runs.Add(new Run("\n", style, link));
                    break;
                case XElement e when e.Name == TextNs + "note":
                    var citation = e.Element(TextNs + "note-citation")?.Value;
                    if (!string.IsNullOrEmpty(citation))
                        runs.Add(new Run(citation, style with { Superscript = true }, link));
                    break;
                case XElement e when e.Name == Draw + "frame":
                    ReadFrame(e, images);
                    break;
                case XElement e when e.Name.Namespace == TextNs && e.HasElements:
                    ReadInline(e, style, link, runs, images);
                    break;
            }
        }
    }

    private void ReadFrame(XElement frame, List<ImageBlock> images)
    {
        var image = frame.Element(Draw + "image");
        if (image is null)
        {
            var textBox = frame.Element(Draw + "text-box");
            if (textBox is not null)
                _doc.Warnings.Add("A text box was flattened into the page.");
            return;
        }

        byte[]? data = null;
        var binary = image.Element(Office + "binary-data")?.Value;
        if (!string.IsNullOrWhiteSpace(binary))
        {
            try
            {
                data = Convert.FromBase64String(binary);
            }
            catch (FormatException)
            {
                data = null;
            }
        }
        else if ((string?)image.Attribute(XLink + "href") is { } href)
        {
            data = _loadPicture(href);
        }

        var mime = DetectImageMime(data);
        if (data is null || mime is null)
        {
            _doc.Warnings.Add("An image in an unsupported format was skipped.");
            return;
        }
        var alt = frame.Element(Svg + "title")?.Value ?? frame.Element(Svg + "desc")?.Value ?? (string?)frame.Attribute(Draw + "name");
        images.Add(new ImageBlock(data, mime, alt) { WidthMm = ParseLengthMm((string?)frame.Attribute(Svg + "width")) });
    }

    public static string? DetectImageMime(byte[]? data)
    {
        if (data is null || data.Length < 4)
            return null;
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
        if (data[0] == 0xFF && data[1] == 0xD8) return "image/jpeg";
        if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F') return "image/gif";
        if (data.Length > 12 && data[0] == 'R' && data[1] == 'I' && data[8] == 'W' && data[9] == 'E') return "image/webp";
        return null;
    }

    private static string CollapseSpaces(string s)
    {
        // In ODF, runs of white space in text content collapse to one space (text:s carries real spacing).
        var sb = new StringBuilder(s.Length);
        var lastSpace = false;
        foreach (var ch in s)
        {
            var space = ch is ' ' or '\n' or '\r' or '\t';
            if (space)
            {
                if (!lastSpace)
                    sb.Append(' ');
            }
            else
            {
                sb.Append(ch);
            }
            lastSpace = space;
        }
        return sb.ToString();
    }

    private TextStyle ResolveText(string? styleName, string family)
    {
        var chain = Chain(styleName);
        var style = TextStyle.Plain;
        if (_defaults.TryGetValue(family, out var def))
            style = ApplyText(style, def.TextProps);
        foreach (var s in chain)
            style = ApplyText(style, s.TextProps);
        return style;
    }

    private ParagraphStyle ResolvePara(string? styleName)
    {
        var para = ParagraphStyle.Default;
        if (_defaults.TryGetValue("paragraph", out var def))
            para = ApplyPara(para, def.ParaProps);
        foreach (var s in Chain(styleName))
            para = ApplyPara(para, s.ParaProps);
        return para;
    }

    private List<StyleDef> Chain(string? name)
    {
        var chain = new List<StyleDef>();
        var guard = 0;
        while (name is not null && _styles.TryGetValue(name, out var s) && guard++ < 32)
        {
            chain.Insert(0, s);
            name = s.Parent;
        }
        return chain;
    }

    private static TextStyle Merge(TextStyle outer, TextStyle inner) => inner == TextStyle.Plain ? outer : new TextStyle
    {
        Bold = outer.Bold || inner.Bold,
        Italic = outer.Italic || inner.Italic,
        Underline = outer.Underline || inner.Underline,
        Strike = outer.Strike || inner.Strike,
        Superscript = inner.Superscript || outer.Superscript,
        Subscript = inner.Subscript || outer.Subscript,
        Monospace = outer.Monospace || inner.Monospace,
        Color = inner.Color ?? outer.Color,
        Highlight = inner.Highlight ?? outer.Highlight,
        FontFamily = inner.FontFamily ?? outer.FontFamily,
        FontSizePt = inner.FontSizePt ?? outer.FontSizePt
    };

    private TextStyle ApplyText(TextStyle s, XElement? p)
    {
        if (p is null)
            return s;
        var weight = (string?)p.Attribute(Fo + "font-weight");
        if (weight is not null)
            s = s with { Bold = weight == "bold" || (int.TryParse(weight, out var w) && w >= 600) };
        var italic = (string?)p.Attribute(Fo + "font-style");
        if (italic is not null)
            s = s with { Italic = italic is "italic" or "oblique" };
        var underline = (string?)p.Attribute(StyleNs + "text-underline-style");
        if (underline is not null)
            s = s with { Underline = underline != "none" };
        var strike = (string?)p.Attribute(StyleNs + "text-line-through-style");
        if (strike is not null)
            s = s with { Strike = strike != "none" };
        var color = (string?)p.Attribute(Fo + "color");
        if (color is not null && color.StartsWith('#'))
            s = s with { Color = color };
        var bg = (string?)p.Attribute(Fo + "background-color");
        if (bg is not null && bg.StartsWith('#'))
            s = s with { Highlight = bg };
        var size = (string?)p.Attribute(Fo + "font-size");
        if (size is not null && size.EndsWith("pt") && double.TryParse(size[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var pt))
            s = s with { FontSizePt = pt };
        var font = (string?)p.Attribute(StyleNs + "font-name") ?? (string?)p.Attribute(Fo + "font-family");
        if (font is not null)
            s = s with { FontFamily = _fontFaces.GetValueOrDefault(font, font.Trim('\'', '"')) };
        var position = (string?)p.Attribute(StyleNs + "text-position");
        if (position is not null)
            s = s with { Superscript = position.StartsWith("super") || position.StartsWith("33%"), Subscript = position.StartsWith("sub") || position.StartsWith("-33%") };
        return s;
    }

    private static ParagraphStyle ApplyPara(ParagraphStyle s, XElement? p)
    {
        if (p is null)
            return s;
        var align = (string?)p.Attribute(Fo + "text-align");
        if (align is not null)
        {
            s = s with
            {
                Align = align switch
                {
                    "center" => TextAlign.Center,
                    "end" or "right" => TextAlign.End,
                    "justify" => TextAlign.Justify,
                    _ => TextAlign.Start
                }
            };
        }
        if (ParseLengthMm((string?)p.Attribute(Fo + "margin-left")) is { } left)
            s = s with { IndentLeftMm = left };
        if (ParseLengthMm((string?)p.Attribute(Fo + "text-indent")) is { } indent)
            s = s with { IndentFirstLineMm = indent };
        if (ParseLengthMm((string?)p.Attribute(Fo + "margin-top")) is { } top)
            s = s with { SpaceBeforePt = top / 25.4 * 72 };
        if (ParseLengthMm((string?)p.Attribute(Fo + "margin-bottom")) is { } bottom)
            s = s with { SpaceAfterPt = bottom / 25.4 * 72 };
        return s;
    }

    /// <summary>Parses an ODF length such as "2cm", "0.5in", "12pt" into millimetres.</summary>
    public static double? ParseLengthMm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        var unitStart = v.Length;
        while (unitStart > 0 && char.IsAsciiLetter(v[unitStart - 1]))
            unitStart--;
        if (!double.TryParse(v[..unitStart], NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return null;
        return v[unitStart..] switch
        {
            "mm" => n,
            "cm" => n * 10,
            "in" => n * 25.4,
            "pt" => n * 25.4 / 72,
            "pc" => n * 25.4 / 6,
            "px" => n * 25.4 / 96,
            _ => null
        };
    }
}
