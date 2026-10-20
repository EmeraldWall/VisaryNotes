using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace VisaryNotes.Core.Documents;

/// <summary>
/// Reads Word documents (.docx, Office Open XML WordprocessingML, ECMA-376) into the shared document model
/// (docs/SPEC.md 7.5). Covers paragraphs, headings, character formatting, lists, tables, links, images,
/// page size and margins, and header and footer text. Tracked deletions and field codes are skipped.
/// </summary>
public sealed class DocxReader
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    private sealed record StyleDef(string Id, string Name, string? BasedOn, XElement? RunProps, XElement? ParaProps);

    private sealed record Relationship(string Target, bool External);

    private readonly ZipArchive _zip;
    private readonly Dictionary<string, StyleDef> _styles = new();
    private readonly Dictionary<string, Relationship> _rels = new();
    private readonly Dictionary<string, Dictionary<int, bool>> _numbering = new();
    private XElement? _defaultRunProps;
    private XElement? _defaultParaProps;
    private int _footnote;
    private readonly Document _doc = new() { Format = DocumentFormat.Docx };

    private DocxReader(ZipArchive zip) => _zip = zip;

    public static Document Read(Stream stream, string fileName)
    {
        var bytes = ReadLimits.ReadAll(stream);
        if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K')
            throw new DocumentReadException("This file is not a Word .docx package. It may be an older .doc file renamed.");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        CheckPackage(zip);
        var doc = new DocxReader(zip).Build();
        if (string.IsNullOrWhiteSpace(doc.Title))
            doc.Title = Path.GetFileNameWithoutExtension(fileName);
        return doc;
    }

    private static void CheckPackage(ZipArchive zip)
    {
        if (zip.Entries.Count > ReadLimits.MaxZipEntries)
            throw new DocumentReadException("The Word package has too many entries.");
        long total = 0;
        foreach (var e in zip.Entries)
        {
            if (e.FullName.Contains("..") || e.FullName.StartsWith('/'))
                throw new DocumentReadException("The Word package contains an unsafe path.");
            total += e.Length;
            if (total > ReadLimits.MaxUncompressedBytes)
                throw new DocumentReadException("The Word package expands to more than the allowed size.");
        }
    }

    private XDocument? Xml(string path)
    {
        var e = _zip.GetEntry(path);
        if (e is null)
            return null;
        using var s = e.Open();
        using var reader = XmlReader.Create(s, OdtReader.SafeXml());
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private byte[]? Bytes(string path)
    {
        var e = _zip.GetEntry(path);
        if (e is null || e.Length > ReadLimits.MaxFileBytes)
            return null;
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private Document Build()
    {
        var mainPath = FindMainPart();
        var main = Xml(mainPath)?.Root ?? throw new DocumentReadException("This file has no document body, so it is not a valid .docx.");
        var dir = mainPath.Contains('/') ? mainPath[..(mainPath.LastIndexOf('/') + 1)] : "";
        var relsPath = $"{dir}_rels/{mainPath[dir.Length..]}.rels";

        foreach (var r in Xml(relsPath)?.Root?.Elements(Rel + "Relationship") ?? [])
        {
            var id = (string?)r.Attribute("Id");
            var target = (string?)r.Attribute("Target");
            if (id is null || target is null)
                continue;
            var external = (string?)r.Attribute("TargetMode") == "External";
            _rels[id] = new Relationship(external ? target : ResolvePath(dir, target), external);
        }

        ReadStyles(Xml(dir + "styles.xml")?.Root);
        ReadNumbering(Xml(dir + "numbering.xml")?.Root);

        var core = Xml("docProps/core.xml")?.Root;
        _doc.Title = core?.Element(Dc + "title")?.Value.Trim() ?? "";
        var creator = core?.Element(Dc + "creator")?.Value.Trim();
        _doc.Author = string.IsNullOrEmpty(creator) ? null : creator;

        var body = main.Element(W + "body") ?? throw new DocumentReadException("This document has no body.");
        ReadBlocks(body, _doc.Blocks, 0);
        ReadSection(body.Element(W + "sectPr") ?? body.Descendants(W + "sectPr").LastOrDefault());
        return _doc;
    }

    /// <summary>The main part is named in the package relationships, usually word/document.xml.</summary>
    private string FindMainPart()
    {
        var target = Xml("_rels/.rels")?.Root?.Elements(Rel + "Relationship")
            .FirstOrDefault(r => ((string?)r.Attribute("Type"))?.EndsWith("/officeDocument", StringComparison.Ordinal) == true)
            ?.Attribute("Target")?.Value;
        return target is null ? "word/document.xml" : ResolvePath("", target);
    }

    private static string ResolvePath(string dir, string target)
    {
        if (target.StartsWith('/'))
            return target.TrimStart('/');
        var parts = new List<string>(dir.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var p in target.Split('/'))
        {
            if (p == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);
            }
            else if (p != "." && p.Length > 0)
            {
                parts.Add(p);
            }
        }
        return string.Join('/', parts);
    }

    private void ReadStyles(XElement? root)
    {
        if (root is null)
            return;
        var defaults = root.Element(W + "docDefaults");
        _defaultRunProps = defaults?.Element(W + "rPrDefault")?.Element(W + "rPr");
        _defaultParaProps = defaults?.Element(W + "pPrDefault")?.Element(W + "pPr");
        foreach (var s in root.Elements(W + "style"))
        {
            var id = (string?)s.Attribute(W + "styleId");
            if (id is null)
                continue;
            _styles[id] = new StyleDef(id, Val(s.Element(W + "name")) ?? id, Val(s.Element(W + "basedOn")),
                s.Element(W + "rPr"), s.Element(W + "pPr"));
        }
    }

    /// <summary>Maps each list (numId) and level to ordered or bulleted. A level with format "none" is not a list.</summary>
    private void ReadNumbering(XElement? root)
    {
        if (root is null)
            return;
        var abstracts = new Dictionary<string, Dictionary<int, bool?>>();
        foreach (var a in root.Elements(W + "abstractNum"))
        {
            var levels = new Dictionary<int, bool?>();
            foreach (var lvl in a.Elements(W + "lvl"))
            {
                var ilvl = (int?)lvl.Attribute(W + "ilvl") ?? 0;
                var fmt = Val(lvl.Element(W + "numFmt")) ?? "decimal";
                levels[ilvl] = fmt switch { "none" => null, "bullet" => false, _ => true };
            }
            abstracts[(string?)a.Attribute(W + "abstractNumId") ?? ""] = levels;
        }
        foreach (var n in root.Elements(W + "num"))
        {
            var id = (string?)n.Attribute(W + "numId");
            var abs = Val(n.Element(W + "abstractNumId"));
            if (id is null || abs is null || !abstracts.TryGetValue(abs, out var levels))
                continue;
            _numbering[id] = levels.Where(l => l.Value.HasValue).ToDictionary(l => l.Key, l => l.Value!.Value);
        }
    }

    private void ReadSection(XElement? sect)
    {
        if (sect is null)
            return;
        var size = sect.Element(W + "pgSz");
        var mar = sect.Element(W + "pgMar");
        double Tw(XElement? e, string a, double fallbackMm) =>
            double.TryParse((string?)e?.Attribute(W + a), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? TwipsToMm(v) : fallbackMm;
        var cols = (int?)sect.Element(W + "cols")?.Attribute(W + "num") ?? 1;
        _doc.Page = new PageSetup(Tw(size, "w", 210), Tw(size, "h", 297), Tw(mar, "top", 25.4), Tw(mar, "right", 25.4),
            Tw(mar, "bottom", 25.4), Tw(mar, "left", 25.4), Math.Max(1, cols));
        _doc.HeaderText = PartText(sect, "headerReference");
        _doc.FooterText = PartText(sect, "footerReference");
    }

    private string? PartText(XElement sect, string reference)
    {
        var refs = sect.Elements(W + reference).ToList();
        var pick = refs.FirstOrDefault(r => (string?)r.Attribute(W + "type") == "default") ?? refs.FirstOrDefault();
        var id = (string?)pick?.Attribute(R + "id");
        if (id is null || !_rels.TryGetValue(id, out var rel) || rel.External)
            return null;
        var root = Xml(rel.Target)?.Root;
        if (root is null)
            return null;
        var text = string.Join(" ", root.Descendants(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)).Trim()).Where(t => t.Length > 0));
        return text.Length == 0 ? null : text;
    }

    private void ReadBlocks(XElement parent, List<Block> into, int depth)
    {
        if (depth > ReadLimits.MaxNestingDepth)
            throw new DocumentReadException("The document is nested too deeply.");

        // Open lists by level, so consecutive list paragraphs gather into one nested list.
        var lists = new List<(ListBlock List, string NumId)>();

        foreach (var el in parent.Elements())
        {
            if (el.Name == W + "p")
            {
                var (numId, level, ordered) = ListInfo(el);
                if (numId is null)
                {
                    lists.Clear();
                    AddParagraph(el, into);
                    continue;
                }

                while (lists.Count > level + 1 || (lists.Count > 0 && lists.Count == level + 1 && lists[^1].NumId != numId))
                    lists.RemoveAt(lists.Count - 1);
                while (lists.Count < level + 1)
                {
                    var list = new ListBlock { Ordered = ordered };
                    if (lists.Count == 0)
                        into.Add(list);
                    else
                    {
                        var parentList = lists[^1].List;
                        if (parentList.Items.Count == 0)
                            parentList.Items.Add(new List<Block>());
                        parentList.Items[^1].Add(list);
                    }
                    lists.Add((list, numId));
                }
                var item = new List<Block>();
                AddParagraph(el, item);
                lists[level].List.Items.Add(item);
            }
            else if (el.Name == W + "tbl")
            {
                lists.Clear();
                into.Add(ReadTable(el, depth));
            }
            else if (el.Name == W + "sdt")
            {
                lists.Clear();
                if (el.Element(W + "sdtContent") is { } content)
                    ReadBlocks(content, into, depth + 1);
            }
            else if (el.Name == W + "customXml" || el.Name == W + "ins" || el.Name == W + "smartTag")
            {
                ReadBlocks(el, into, depth + 1);
            }
        }
    }

    private (string? NumId, int Level, bool Ordered) ListInfo(XElement p)
    {
        var numPr = p.Element(W + "pPr")?.Element(W + "numPr");
        string? numId = Val(numPr?.Element(W + "numId"));
        var level = int.TryParse(Val(numPr?.Element(W + "ilvl")), out var l) ? l : 0;
        if (numId is null)
        {
            // Numbering can also come from the paragraph style, as with Word's "List Bullet".
            foreach (var s in Chain(Val(p.Element(W + "pPr")?.Element(W + "pStyle"))))
            {
                var sn = s.ParaProps?.Element(W + "numPr");
                if (sn is null)
                    continue;
                numId = Val(sn.Element(W + "numId")) ?? numId;
                if (int.TryParse(Val(sn.Element(W + "ilvl")), out var sl))
                    level = sl;
            }
        }
        level = Math.Clamp(level, 0, 8);
        if (numId is null or "0" || !_numbering.TryGetValue(numId, out var levels) || !levels.TryGetValue(level, out var ordered))
            return (null, 0, false);
        return (numId, level, ordered);
    }

    private TableBlock ReadTable(XElement tbl, int depth)
    {
        var block = new TableBlock();
        // Cells that continue a vertical merge are dropped, and the cell that started it spans one more row.
        var openMerges = new Dictionary<int, TableCell>();
        foreach (var tr in tbl.Elements(W + "tr"))
        {
            var header = tr.Element(W + "trPr")?.Element(W + "tblHeader") is { } th && IsOn(th);
            var row = new List<TableCell>();
            var column = 0;
            foreach (var tc in tr.Elements().SelectMany(e => e.Name == W + "sdt" ? e.Element(W + "sdtContent")?.Elements() ?? [] : [e]).Where(e => e.Name == W + "tc"))
            {
                var props = tc.Element(W + "tcPr");
                var span = Math.Clamp((int?)props?.Element(W + "gridSpan")?.Attribute(W + "val") ?? 1, 1, 64);
                var vMerge = props?.Element(W + "vMerge");
                var mergeVal = (string?)vMerge?.Attribute(W + "val");
                if (vMerge is not null && mergeVal != "restart" && openMerges.TryGetValue(column, out var above))
                {
                    above.RowSpan++;
                    column += span;
                    continue;
                }
                var cell = new TableCell { ColumnSpan = span, IsHeader = header };
                ReadBlocks(tc, cell.Blocks, depth + 1);
                if (mergeVal == "restart")
                    openMerges[column] = cell;
                else
                    openMerges.Remove(column);
                row.Add(cell);
                column += span;
            }
            if (row.Count > 0)
                block.Rows.Add(row);
        }
        return block;
    }

    private void AddParagraph(XElement p, List<Block> into)
    {
        var pPr = p.Element(W + "pPr");
        var styleId = Val(pPr?.Element(W + "pStyle"));
        var chain = Chain(styleId);
        var baseRun = ResolveRun(chain, null);
        var para = ResolvePara(chain, pPr);

        var runs = new List<Run>();
        var images = new List<ImageBlock>();
        var pageBreak = false;
        ReadInline(p, baseRun, null, runs, images, ref pageBreak);

        var level = HeadingLevel(chain, pPr);
        if (level is not null && runs.Any(r => !string.IsNullOrWhiteSpace(r.Text)))
        {
            var h = new Heading(level.Value) { Style = para };
            h.Runs.AddRange(runs);
            into.Add(h);
        }
        else if (runs.Count > 0 || images.Count == 0)
        {
            var para2 = new Paragraph { Style = para };
            para2.Runs.AddRange(runs);
            into.Add(para2);
        }
        into.AddRange(images);
        if (pageBreak)
            into.Add(new PageBreak());
    }

    private int? HeadingLevel(List<StyleDef> chain, XElement? pPr)
    {
        if (int.TryParse(Val(pPr?.Element(W + "outlineLvl")), out var direct) && direct < 9)
            return direct + 1;
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var name = chain[i].Name.ToLowerInvariant();
            if (name == "title")
                return 1;
            if (name.StartsWith("heading ", StringComparison.Ordinal) && int.TryParse(name[8..], out var n))
                return n;
            if (int.TryParse(Val(chain[i].ParaProps?.Element(W + "outlineLvl")), out var o) && o < 9)
                return o + 1;
        }
        return null;
    }

    private void ReadInline(XElement parent, TextStyle style, string? link, List<Run> runs, List<ImageBlock> images, ref bool pageBreak)
    {
        foreach (var el in parent.Elements())
        {
            var name = el.Name;
            if (name == W + "r")
            {
                var runStyle = ResolveRun(Chain(Val(el.Element(W + "rPr")?.Element(W + "rStyle"))), el.Element(W + "rPr"), style);
                foreach (var c in el.Elements())
                {
                    if (c.Name == W + "t")
                        runs.Add(new Run(c.Value, runStyle, link));
                    else if (c.Name == W + "tab")
                        runs.Add(new Run("\t", runStyle, link));
                    else if (c.Name == W + "br" || c.Name == W + "cr")
                    {
                        if ((string?)c.Attribute(W + "type") == "page")
                            pageBreak = true;
                        else
                            runs.Add(new Run("\n", runStyle, link));
                    }
                    else if (c.Name == W + "noBreakHyphen")
                        runs.Add(new Run("‑", runStyle, link));
                    else if (c.Name == W + "sym" && int.TryParse((string?)c.Attribute(W + "char"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && code is > 0x1F and < 0xF000)
                        runs.Add(new Run(char.ConvertFromUtf32(code), runStyle, link));
                    else if (c.Name == W + "footnoteReference" || c.Name == W + "endnoteReference")
                        runs.Add(new Run((++_footnote).ToString(CultureInfo.InvariantCulture), runStyle with { Superscript = true }, link));
                    else if (c.Name == W + "drawing" || c.Name == W + "pict" || c.Name.LocalName == "AlternateContent")
                        ReadImages(c, images);
                    // w:instrText (field codes), w:delText (tracked deletions) and others are skipped on purpose.
                }
            }
            else if (name == W + "hyperlink")
            {
                var id = (string?)el.Attribute(R + "id");
                string? href = null;
                if (id is not null && _rels.TryGetValue(id, out var rel) && rel.External
                    && Uri.TryCreate(rel.Target, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto")
                    href = rel.Target;
                ReadInline(el, style, href ?? link, runs, images, ref pageBreak);
            }
            else if (name == W + "ins" || name == W + "smartTag" || name == W + "customXml" || name == W + "fldSimple" || name == W + "moveTo")
            {
                ReadInline(el, style, link, runs, images, ref pageBreak);
            }
            else if (name == W + "sdt")
            {
                if (el.Element(W + "sdtContent") is { } content)
                    ReadInline(content, style, link, runs, images, ref pageBreak);
            }
            // w:del and w:moveFrom hold tracked deletions and are skipped.
        }
    }

    private void ReadImages(XElement container, List<ImageBlock> images)
    {
        foreach (var blip in container.Descendants(A + "blip"))
        {
            var id = (string?)blip.Attribute(R + "embed");
            var extent = blip.Ancestors().Select(a => a.Element(WP + "extent")).FirstOrDefault(e => e is not null);
            var docPr = blip.Ancestors().Select(a => a.Element(WP + "docPr")).FirstOrDefault(e => e is not null);
            var alt = (string?)docPr?.Attribute("descr");
            if (string.IsNullOrWhiteSpace(alt))
                alt = (string?)docPr?.Attribute("title") ?? (string?)docPr?.Attribute("name");
            AddImage(id, alt, long.TryParse((string?)extent?.Attribute("cx"), out var cx) ? cx / 36000.0 : null, images);
            return;
        }
        foreach (var data in container.Descendants(V + "imagedata"))
        {
            AddImage((string?)data.Attribute(R + "id"), (string?)data.Attribute("title"), null, images);
            return;
        }
    }

    private void AddImage(string? relId, string? alt, double? widthMm, List<ImageBlock> images)
    {
        if (relId is null || !_rels.TryGetValue(relId, out var rel) || rel.External)
            return;
        var data = Bytes(rel.Target);
        var mime = OdtReader.DetectImageMime(data);
        if (data is null || mime is null)
        {
            _doc.Warnings.Add("An image in an unsupported format was skipped.");
            return;
        }
        images.Add(new ImageBlock(data, mime, alt) { WidthMm = widthMm });
    }

    private List<StyleDef> Chain(string? id)
    {
        var chain = new List<StyleDef>();
        var guard = 0;
        while (id is not null && _styles.TryGetValue(id, out var s) && guard++ < 32)
        {
            chain.Insert(0, s);
            id = s.BasedOn;
        }
        return chain;
    }

    private TextStyle ResolveRun(List<StyleDef> chain, XElement? direct, TextStyle? outer = null)
    {
        var s = outer ?? ApplyRun(TextStyle.Plain, _defaultRunProps);
        foreach (var def in chain)
            s = ApplyRun(s, def.RunProps);
        return ApplyRun(s, direct);
    }

    private static TextStyle ApplyRun(TextStyle s, XElement? rPr)
    {
        if (rPr is null)
            return s;
        if (rPr.Element(W + "b") is { } b)
            s = s with { Bold = IsOn(b) };
        if (rPr.Element(W + "i") is { } i)
            s = s with { Italic = IsOn(i) };
        if (rPr.Element(W + "u") is { } u)
            s = s with { Underline = (string?)u.Attribute(W + "val") != "none" };
        if (rPr.Element(W + "strike") is { } st)
            s = s with { Strike = IsOn(st) };
        else if (rPr.Element(W + "dstrike") is { } ds)
            s = s with { Strike = IsOn(ds) };
        if (Val(rPr.Element(W + "vertAlign")) is { } va)
            s = s with { Superscript = va == "superscript", Subscript = va == "subscript" };
        if (Val(rPr.Element(W + "color")) is { } color && color.Length == 6 && color != "auto")
            s = s with { Color = "#" + color };
        if (Val(rPr.Element(W + "highlight")) is { } hl && HighlightColor(hl) is { } hex)
            s = s with { Highlight = hex };
        else if ((string?)rPr.Element(W + "shd")?.Attribute(W + "fill") is { Length: 6 } fill && fill != "auto")
            s = s with { Highlight = "#" + fill };
        if (double.TryParse(Val(rPr.Element(W + "sz")), NumberStyles.Float, CultureInfo.InvariantCulture, out var half))
            s = s with { FontSizePt = half / 2 };
        if ((string?)rPr.Element(W + "rFonts")?.Attribute(W + "ascii") is { } font)
            s = s with { FontFamily = font };
        return s;
    }

    private ParagraphStyle ResolvePara(List<StyleDef> chain, XElement? direct)
    {
        var s = ApplyPara(ParagraphStyle.Default, _defaultParaProps);
        foreach (var def in chain)
            s = ApplyPara(s, def.ParaProps);
        return ApplyPara(s, direct);
    }

    private static ParagraphStyle ApplyPara(ParagraphStyle s, XElement? pPr)
    {
        if (pPr is null)
            return s;
        if (Val(pPr.Element(W + "jc")) is { } jc)
        {
            s = s with
            {
                Align = jc switch
                {
                    "center" => TextAlign.Center,
                    "right" or "end" => TextAlign.End,
                    "both" or "distribute" => TextAlign.Justify,
                    _ => TextAlign.Start
                }
            };
        }
        if (pPr.Element(W + "ind") is { } ind)
        {
            if ((Twips(ind, "left") ?? Twips(ind, "start")) is { } left)
                s = s with { IndentLeftMm = TwipsToMm(left) };
            if (Twips(ind, "firstLine") is { } first)
                s = s with { IndentFirstLineMm = TwipsToMm(first) };
            else if (Twips(ind, "hanging") is { } hanging)
                s = s with { IndentFirstLineMm = -TwipsToMm(hanging) };
        }
        if (pPr.Element(W + "spacing") is { } sp)
        {
            if (Twips(sp, "before") is { } before)
                s = s with { SpaceBeforePt = before / 20 };
            if (Twips(sp, "after") is { } after)
                s = s with { SpaceAfterPt = after / 20 };
        }
        return s;
    }

    private static double? Twips(XElement e, string attr) =>
        double.TryParse((string?)e.Attribute(W + attr), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double TwipsToMm(double twips) => twips * 25.4 / 1440;

    private static string? Val(XElement? e) => (string?)e?.Attribute(W + "val");

    /// <summary>On/off properties are on when present, unless their value is false, 0 or off.</summary>
    private static bool IsOn(XElement e) => (string?)e.Attribute(W + "val") is not ("0" or "false" or "off");

    private static string? HighlightColor(string name) => name switch
    {
        "yellow" => "#FFFF00",
        "green" => "#00FF00",
        "cyan" => "#00FFFF",
        "magenta" => "#FF00FF",
        "blue" => "#0000FF",
        "red" => "#FF0000",
        "darkBlue" => "#000080",
        "darkCyan" => "#008080",
        "darkGreen" => "#008000",
        "darkMagenta" => "#800080",
        "darkRed" => "#800000",
        "darkYellow" => "#808000",
        "darkGray" => "#808080",
        "lightGray" => "#C0C0C0",
        "black" => "#000000",
        "white" => "#FFFFFF",
        _ => null
    };
}
