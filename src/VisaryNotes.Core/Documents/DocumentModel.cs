namespace VisaryNotes.Core.Documents;

/// <summary>
/// The common tree every reader produces and every renderer and exporter consumes (docs/SPEC.md 7.6).
/// </summary>
public sealed class Document
{
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public DocumentFormat Format { get; set; }
    public PageSetup Page { get; set; } = PageSetup.A4;
    public List<Block> Blocks { get; } = new();

    /// <summary>Page sizes of a fixed layout document (PDF). Original view draws these pages as images.</summary>
    public List<PageSetup> FixedPages { get; } = new();

    /// <summary>Notes produced while reading, such as substituted fonts or skipped objects.</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>Header and footer text from the original layout, shown in Original view only.</summary>
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }

    public string PlainText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in Blocks)
            b.AppendPlainText(sb);
        return sb.ToString().Trim();
    }
}

public enum DocumentFormat
{
    Markdown,
    PlainText,
    Rtf,
    Odt,
    Docx,
    Doc,
    Pdf
}

/// <summary>Page size and margins in millimetres.</summary>
public sealed record PageSetup(double WidthMm, double HeightMm, double MarginTopMm, double MarginRightMm, double MarginBottomMm, double MarginLeftMm, int Columns = 1)
{
    public static PageSetup A4 { get; } = new(210, 297, 20, 20, 20, 20);
    public static PageSetup Letter { get; } = new(215.9, 279.4, 25.4, 25.4, 25.4, 25.4);
}

public enum TextAlign
{
    Start,
    Center,
    End,
    Justify
}

/// <summary>Formatting of one run of text. Null means "inherit".</summary>
public sealed record TextStyle
{
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool Strike { get; init; }
    public bool Superscript { get; init; }
    public bool Subscript { get; init; }
    public bool Monospace { get; init; }
    public string? Color { get; init; }
    public string? Highlight { get; init; }
    public string? FontFamily { get; init; }
    public double? FontSizePt { get; init; }

    public static TextStyle Plain { get; } = new();
}

/// <summary>Paragraph level formatting, used by Original view.</summary>
public sealed record ParagraphStyle
{
    public TextAlign Align { get; init; } = TextAlign.Start;
    public double IndentLeftMm { get; init; }
    public double IndentFirstLineMm { get; init; }
    public double SpaceBeforePt { get; init; }
    public double SpaceAfterPt { get; init; }

    public static ParagraphStyle Default { get; } = new();
}

public sealed record Run(string Text, TextStyle Style, string? Link = null);

public abstract class Block
{
    internal abstract void AppendPlainText(System.Text.StringBuilder sb);
}

public sealed class Paragraph : Block
{
    public List<Run> Runs { get; } = new();
    public ParagraphStyle Style { get; set; } = ParagraphStyle.Default;

    public bool IsEmpty => Runs.All(r => string.IsNullOrWhiteSpace(r.Text));

    public string Text => string.Concat(Runs.Select(r => r.Text));

    internal override void AppendPlainText(System.Text.StringBuilder sb) => sb.AppendLine(Text).AppendLine();
}

public sealed class Heading : Block
{
    public Heading(int level) => Level = Math.Clamp(level, 1, 6);

    public int Level { get; }
    public List<Run> Runs { get; } = new();
    public ParagraphStyle Style { get; set; } = ParagraphStyle.Default;

    public string Text => string.Concat(Runs.Select(r => r.Text));

    internal override void AppendPlainText(System.Text.StringBuilder sb) => sb.AppendLine(Text).AppendLine();
}

public sealed class ListBlock : Block
{
    public bool Ordered { get; set; }
    public List<List<Block>> Items { get; } = new();

    internal override void AppendPlainText(System.Text.StringBuilder sb)
    {
        foreach (var item in Items)
            foreach (var b in item)
                b.AppendPlainText(sb);
    }
}

public sealed class TableBlock : Block
{
    public List<List<TableCell>> Rows { get; } = new();

    internal override void AppendPlainText(System.Text.StringBuilder sb)
    {
        foreach (var row in Rows)
            sb.AppendLine(string.Join("\t", row.Select(c => string.Concat(c.Blocks.OfType<Paragraph>().Select(p => p.Text)))));
        sb.AppendLine();
    }
}

public sealed class TableCell
{
    public List<Block> Blocks { get; } = new();
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public bool IsHeader { get; set; }
}

public sealed class ImageBlock : Block
{
    public ImageBlock(byte[] data, string mimeType, string? alt = null)
    {
        Data = data;
        MimeType = mimeType;
        Alt = alt;
    }

    public byte[] Data { get; }
    public string MimeType { get; }
    public string? Alt { get; }
    public double? WidthMm { get; set; }

    internal override void AppendPlainText(System.Text.StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(Alt))
            sb.AppendLine($"[{Alt}]").AppendLine();
    }
}

/// <summary>Text that must keep its line breaks and spacing, such as plain text files in Original view.</summary>
public sealed class Preformatted : Block
{
    public Preformatted(string text) => Text = text;

    public string Text { get; }

    internal override void AppendPlainText(System.Text.StringBuilder sb) => sb.AppendLine(Text).AppendLine();
}

public sealed class Quote : Block
{
    public List<Block> Blocks { get; } = new();

    internal override void AppendPlainText(System.Text.StringBuilder sb)
    {
        foreach (var b in Blocks)
            b.AppendPlainText(sb);
    }
}

public sealed class HorizontalRule : Block
{
    internal override void AppendPlainText(System.Text.StringBuilder sb) => sb.AppendLine("---").AppendLine();
}

public sealed class PageBreak : Block
{
    internal override void AppendPlainText(System.Text.StringBuilder sb) { }
}
