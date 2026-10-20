using System.IO.Compression;
using System.Security;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using VisaryNotes.Core.Rendering;

namespace VisaryNotes.Core.Export;

/// <summary>
/// Writes a Markdown note as a Word document (.docx, ECMA-376), so a note can go back to people in the
/// format they expect (docs/STRATEGY.md D7). Headings, paragraphs, bold, italic, strike, inline code,
/// links, bulleted and numbered lists with nesting, task lists, quotes, code blocks, rules and tables are
/// mapped to built in Word styles, so the file looks native when opened in Word or LibreOffice.
/// </summary>
public static class DocxWriter
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] FromMarkdown(string markdown, string title)
    {
        var doc = Markdown.Parse(markdown, MarkdownRenderer.Pipeline);
        var w = new Writer();
        foreach (var block in doc)
            w.Block(block, 0, null);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text)
            {
                using var s = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                s.Write(text);
            }
            Add("[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
                <Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
                <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
                </Types>
                """);
            Add("_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
                </Relationships>
                """);
            Add("docProps/core.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>{X(title)}</dc:title></cp:coreProperties>
                """);
            Add("word/_rels/document.xml.rels", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rIdStyles" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                <Relationship Id="rIdNumbering" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/>
                {w.Links}</Relationships>
                """);
            Add("word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="{W}" xmlns:r="{R}"><w:body>{w.Body}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr></w:body></w:document>
                """);
            Add("word/styles.xml", Styles);
            Add("word/numbering.xml", Numbering(w.OrderedLists));
        }
        return ms.ToArray();
    }

    private sealed class Writer
    {
        public StringBuilder Body { get; } = new();
        public StringBuilder Links { get; } = new();
        /// <summary>Each numbered list gets its own numId so its count restarts at 1.</summary>
        public int OrderedLists { get; private set; }
        private int _links;

        public void Block(Block block, int level, string? numId)
        {
            switch (block)
            {
                case HeadingBlock h:
                    Paragraph($"Heading{Math.Clamp(h.Level, 1, 6)}", h.Inline);
                    break;
                case ParagraphBlock p:
                    Paragraph(null, p.Inline);
                    break;
                case ListBlock list:
                    var id = list.IsOrdered ? (2 + OrderedLists++).ToString() : "1";
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var first = true;
                        foreach (var child in item)
                        {
                            if (first && child is ParagraphBlock lp)
                                Paragraph("ListParagraph", lp.Inline, (id, level));
                            else
                                Block(child, child is ListBlock ? level + 1 : level, id);
                            first = false;
                        }
                    }
                    break;
                case QuoteBlock q:
                    foreach (var child in q)
                    {
                        if (child is ParagraphBlock qp)
                            Paragraph("Quote", qp.Inline);
                        else
                            Block(child, level, numId);
                    }
                    break;
                case CodeBlock code:
                    foreach (var line in code.Lines.Lines.Take(code.Lines.Count))
                        Body.Append("<w:p><w:pPr><w:pStyle w:val=\"Code\"/></w:pPr>").Append(Text(line.ToString())).Append("</w:p>");
                    break;
                case ThematicBreakBlock:
                    Body.Append("<w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"6\" w:space=\"1\" w:color=\"999999\"/></w:pBdr></w:pPr></w:p>");
                    break;
                case Table table:
                    Table(table);
                    break;
                case ContainerBlock container:
                    foreach (var child in container)
                        Block(child, level, numId);
                    break;
            }
        }

        private void Table(Table table)
        {
            Body.Append("<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"5000\" w:type=\"pct\"/></w:tblPr>");
            foreach (var row in table.OfType<TableRow>())
            {
                Body.Append("<w:tr>");
                if (row.IsHeader)
                    Body.Replace("<w:tr>", "<w:tr><w:trPr><w:tblHeader/></w:trPr>", Body.Length - 6, 6);
                foreach (var cell in row.OfType<TableCell>())
                {
                    Body.Append("<w:tc>");
                    var any = false;
                    foreach (var p in cell.OfType<ParagraphBlock>())
                    {
                        Paragraph(null, p.Inline, bold: row.IsHeader);
                        any = true;
                    }
                    if (!any)
                        Body.Append("<w:p/>");
                    Body.Append("</w:tc>");
                }
                Body.Append("</w:tr>");
            }
            Body.Append("</w:tbl><w:p/>");
        }

        private void Paragraph(string? style, ContainerInline? inline, (string NumId, int Level)? list = null, bool bold = false)
        {
            Body.Append("<w:p>");
            if (style is not null || list is not null)
            {
                Body.Append("<w:pPr>");
                if (style is not null)
                    Body.Append("<w:pStyle w:val=\"").Append(style).Append("\"/>");
                if (list is { } l)
                    Body.Append("<w:numPr><w:ilvl w:val=\"").Append(Math.Min(l.Level, 8)).Append("\"/><w:numId w:val=\"").Append(l.NumId).Append("\"/></w:numPr>");
                Body.Append("</w:pPr>");
            }
            if (inline is not null)
                Inlines(inline, new Fmt(bold, false, false, false), null);
            Body.Append("</w:p>");
        }

        private readonly record struct Fmt(bool Bold, bool Italic, bool Strike, bool Code);

        private void Inlines(ContainerInline container, Fmt fmt, string? linkId)
        {
            foreach (var inline in container)
            {
                switch (inline)
                {
                    case LiteralInline lit:
                        Run(lit.Content.ToString(), fmt, linkId);
                        break;
                    case EmphasisInline em:
                        var next = em.DelimiterChar == '~' ? fmt with { Strike = true }
                            : em.DelimiterCount >= 2 ? fmt with { Bold = true } : fmt with { Italic = true };
                        Inlines(em, next, linkId);
                        break;
                    case CodeInline code:
                        Run(code.Content, fmt with { Code = true }, linkId);
                        break;
                    case TaskList task:
                        Run(task.Checked ? "☑" : "☐", fmt, linkId);
                        break;
                    case LinkInline { IsImage: true } image:
                        Run($"[{string.Concat(image.Descendants<LiteralInline>().Select(l => l.Content.ToString()))}]", fmt with { Italic = true }, linkId);
                        break;
                    case LinkInline link when Uri.TryCreate(link.Url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto":
                        var id = $"rIdLink{++_links}";
                        Links.Append("<Relationship Id=\"").Append(id).Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"")
                            .Append(X(link.Url!)).Append("\" TargetMode=\"External\"/>");
                        Body.Append("<w:hyperlink r:id=\"").Append(id).Append("\">");
                        Inlines(link, fmt, id);
                        Body.Append("</w:hyperlink>");
                        break;
                    case LinkInline other:
                        Inlines(other, fmt, linkId);
                        break;
                    case LineBreakInline br:
                        if (br.IsHard)
                            Body.Append("<w:r><w:br/></w:r>");
                        else
                            Run(" ", fmt, linkId);
                        break;
                    case ContainerInline nested:
                        Inlines(nested, fmt, linkId);
                        break;
                    case AutolinkInline auto:
                        Run(auto.Url, fmt, linkId);
                        break;
                    case HtmlEntityInline entity:
                        Run(entity.Transcoded.ToString(), fmt, linkId);
                        break;
                }
            }
        }

        private void Run(string text, Fmt fmt, string? linkId)
        {
            if (text.Length == 0)
                return;
            Body.Append("<w:r>");
            if (fmt.Bold || fmt.Italic || fmt.Strike || fmt.Code || linkId is not null)
            {
                Body.Append("<w:rPr>");
                if (linkId is not null) Body.Append("<w:rStyle w:val=\"Hyperlink\"/>");
                else if (fmt.Code) Body.Append("<w:rStyle w:val=\"CodeChar\"/>");
                if (fmt.Bold) Body.Append("<w:b/>");
                if (fmt.Italic) Body.Append("<w:i/>");
                if (fmt.Strike) Body.Append("<w:strike/>");
                Body.Append("</w:rPr>");
            }
            Body.Append(Text(text)).Append("</w:r>");
        }

        private static string Text(string text) => $"<w:t xml:space=\"preserve\">{X(text)}</w:t>";
    }

    private static string X(string s) => SecurityElement.Escape(new string(s.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray())) ?? "";

    private static string Numbering(int orderedLists)
    {
        var sb = new StringBuilder($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:numbering xmlns:w="{W}">""");
        sb.Append("<w:abstractNum w:abstractNumId=\"0\"><w:multiLevelType w:val=\"hybridMultilevel\"/>");
        for (var i = 0; i < 9; i++)
            sb.Append($"<w:lvl w:ilvl=\"{i}\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"{(i % 2 == 0 ? "•" : "◦")}\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"{720 + i * 360}\" w:hanging=\"360\"/></w:pPr></w:lvl>");
        sb.Append("</w:abstractNum><w:abstractNum w:abstractNumId=\"1\"><w:multiLevelType w:val=\"hybridMultilevel\"/>");
        for (var i = 0; i < 9; i++)
            sb.Append($"<w:lvl w:ilvl=\"{i}\"><w:start w:val=\"1\"/><w:numFmt w:val=\"{(i % 3 == 0 ? "decimal" : i % 3 == 1 ? "lowerLetter" : "lowerRoman")}\"/><w:lvlText w:val=\"%{i + 1}.\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"{720 + i * 360}\" w:hanging=\"360\"/></w:pPr></w:lvl>");
        sb.Append("</w:abstractNum>");
        sb.Append("<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>");
        for (var n = 0; n < orderedLists; n++)
            sb.Append($"<w:num w:numId=\"{n + 2}\"><w:abstractNumId w:val=\"1\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"1\"/></w:lvlOverride></w:num>");
        return sb.Append("</w:numbering>").ToString();
    }

    private static readonly string Styles = $$"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="{{W}}">
        <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/><w:sz w:val="22"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="160" w:line="276" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
        <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
        <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="360" w:after="120"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="36"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="240" w:after="80"/><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="30"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Heading3"><w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="200" w:after="80"/><w:outlineLvl w:val="2"/></w:pPr><w:rPr><w:b/><w:sz w:val="26"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Heading4"><w:name w:val="heading 4"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:outlineLvl w:val="3"/></w:pPr><w:rPr><w:b/><w:i/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Heading5"><w:name w:val="heading 5"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:outlineLvl w:val="4"/></w:pPr><w:rPr><w:b/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Heading6"><w:name w:val="heading 6"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:outlineLvl w:val="5"/></w:pPr><w:rPr><w:i/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="ListParagraph"><w:name w:val="List Paragraph"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="60"/><w:contextualSpacing/></w:pPr></w:style>
        <w:style w:type="paragraph" w:styleId="Quote"><w:name w:val="Quote"/><w:basedOn w:val="Normal"/><w:pPr><w:pBdr><w:left w:val="single" w:sz="18" w:space="8" w:color="0A6249"/></w:pBdr><w:ind w:left="284"/></w:pPr><w:rPr><w:i/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Code"><w:name w:val="Code"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/><w:shd w:val="clear" w:color="auto" w:fill="F2F2F2"/></w:pPr><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="20"/></w:rPr></w:style>
        <w:style w:type="character" w:styleId="CodeChar"><w:name w:val="Code Char"/><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:shd w:val="clear" w:color="auto" w:fill="F2F2F2"/></w:rPr></w:style>
        <w:style w:type="character" w:styleId="Hyperlink"><w:name w:val="Hyperlink"/><w:rPr><w:color w:val="0A6249"/><w:u w:val="single"/></w:rPr></w:style>
        <w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:color="999999"/><w:left w:val="single" w:sz="4" w:color="999999"/><w:bottom w:val="single" w:sz="4" w:color="999999"/><w:right w:val="single" w:sz="4" w:color="999999"/><w:insideH w:val="single" w:sz="4" w:color="999999"/><w:insideV w:val="single" w:sz="4" w:color="999999"/></w:tblBorders><w:tblCellMar><w:left w:w="108" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style>
        </w:styles>
        """;
}
