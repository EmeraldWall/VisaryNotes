using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Exceptions;

namespace VisaryNotes.Core.Documents;

/// <summary>
/// Reads PDF files with PdfPig (Apache 2.0). Reading view gets the text, reflowed: words are grouped into
/// blocks (Docstrum), blocks are put in reading order, larger text becomes headings and words split by a
/// hyphen at a line end are joined again. Original view does not use these blocks: the app draws the real
/// pages with the Android PDF renderer, using the page sizes kept in <see cref="Document.FixedPages"/>.
/// </summary>
public static class PdfReader
{
    /// <summary>Text extraction stops here; Original view still shows every page.</summary>
    public const int MaxReflowPages = 1500;

    private const double PointsToMm = 25.4 / 72;

    public static Document Read(Stream stream, string fileName)
    {
        var bytes = ReadLimits.ReadAll(stream);
        if (bytes.Length < 5 || !System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024)).Contains("%PDF-"))
            throw new DocumentReadException("This file is not a PDF.");

        PdfDocument pdf;
        try
        {
            pdf = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true });
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new DocumentReadException("This PDF is password protected.", ex);
        }
        catch (Exception ex) when (ex is not DocumentReadException)
        {
            throw new DocumentReadException("This PDF is damaged and could not be opened.", ex);
        }

        using (pdf)
        {
            var doc = new Document { Format = DocumentFormat.Pdf };
            var title = pdf.Information.Title?.Trim();
            doc.Title = string.IsNullOrEmpty(title) ? Path.GetFileNameWithoutExtension(fileName) : title;
            var author = pdf.Information.Author?.Trim();
            doc.Author = string.IsNullOrEmpty(author) ? null : author;

            var pages = new List<(List<(TextBlock Block, double Size)> Blocks, List<(double Top, ImageBlock Image)> Images)>();
            var sizes = new Dictionary<int, long>();
            for (var n = 1; n <= pdf.NumberOfPages; n++)
            {
                Page page;
                try
                {
                    page = pdf.GetPage(n);
                }
                catch (Exception)
                {
                    doc.Warnings.Add($"Page {n} could not be read.");
                    doc.FixedPages.Add(PageSetup.A4 with { MarginTopMm = 0, MarginRightMm = 0, MarginBottomMm = 0, MarginLeftMm = 0 });
                    continue;
                }
                doc.FixedPages.Add(new PageSetup(page.Width * PointsToMm, page.Height * PointsToMm, 0, 0, 0, 0));
                if (n > MaxReflowPages)
                    continue;
                pages.Add(ReadPage(page, sizes));
            }
            if (pdf.NumberOfPages > MaxReflowPages)
                doc.Warnings.Add($"Reading view shows the first {MaxReflowPages} pages. Original view shows them all.");

            var body = HistogramMedian(sizes);
            foreach (var (blocks, images) in pages)
            {
                var imageIndex = 0;
                foreach (var (block, size) in blocks)
                {
                    // Images sit between the blocks by their position on the page.
                    while (imageIndex < images.Count && images[imageIndex].Top >= block.BoundingBox.Top)
                        doc.Blocks.Add(images[imageIndex++].Image);
                    AddBlock(doc, block, body);
                }
                while (imageIndex < images.Count)
                    doc.Blocks.Add(images[imageIndex++].Image);
            }

            if (!doc.Blocks.OfType<Paragraph>().Any() && !doc.Blocks.OfType<Heading>().Any())
                doc.Warnings.Add("This PDF has no text layer, so it is probably scanned. Original view shows its pages.");
            return doc;
        }
    }

    private static (List<(TextBlock, double)>, List<(double, ImageBlock)>) ReadPage(Page page, Dictionary<int, long> sizes)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToList();
        var blocks = new List<(TextBlock, double)>();
        if (words.Count > 0)
        {
            var segmented = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            foreach (var block in UnsupervisedReadingOrderDetector.Instance.Get(segmented))
            {
                var letterSizes = block.TextLines.SelectMany(l => l.Words).SelectMany(w => w.Letters).Select(l => l.PointSize).Where(s => s > 0).ToList();
                var size = Median(letterSizes);
                foreach (var s in letterSizes)
                {
                    var bucket = (int)Math.Round(s * 2);
                    sizes[bucket] = sizes.GetValueOrDefault(bucket) + 1;
                }
                blocks.Add((block, size));
            }
        }

        var images = new List<(double, ImageBlock)>();
        try
        {
            foreach (var img in page.GetImages())
            {
                // Skip rules, bullets and other tiny decorations.
                if (img.BoundingBox.Width < 48 || img.BoundingBox.Height < 48)
                    continue;
                byte[]? data = null;
                if (img.TryGetPng(out var png))
                    data = png;
                else if (img.RawBytes.Length > 2 && img.RawBytes[0] == 0xFF && img.RawBytes[1] == 0xD8)
                    data = img.RawBytes.ToArray();
                var mime = OdtReader.DetectImageMime(data);
                if (data is null || mime is null)
                    continue;
                images.Add((img.BoundingBox.Top, new ImageBlock(data, mime) { WidthMm = img.BoundingBox.Width * PointsToMm }));
            }
        }
        catch (Exception)
        {
            // An unreadable image never stops the text from showing.
        }
        images.Sort((a, b) => b.Item1.CompareTo(a.Item1));
        return (blocks, images);
    }

    private sealed record Line(string Text, double Size, double Top, double Bottom, bool Bold);

    /// <summary>
    /// Splits a block into paragraphs. Page segmentation often merges a heading, its paragraph and a list
    /// into one block, so a new paragraph starts where the font size changes, the gap between lines grows,
    /// or a line begins with a list marker.
    /// </summary>
    private static void AddBlock(Document doc, TextBlock block, double body)
    {
        var lines = block.TextLines
            .Select(l =>
            {
                var letters = l.Words.SelectMany(w => w.Letters).ToList();
                var size = Median(letters.Select(x => x.PointSize).Where(x => x > 0).ToList());
                var bold = letters.Count(x => x.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true) * 2 > letters.Count;
                return new Line(l.Text.Trim(), size, l.BoundingBox.Top, l.BoundingBox.Bottom, bold);
            })
            .Where(l => l.Text.Length > 0)
            .ToList();
        if (lines.Count == 0)
            return;

        var gaps = new List<double>();
        for (var i = 1; i < lines.Count; i++)
            gaps.Add(lines[i - 1].Bottom - lines[i].Top);
        var typicalGap = Median(gaps.Where(g => g >= 0).ToList());

        var current = new List<Line> { lines[0] };
        for (var i = 1; i < lines.Count; i++)
        {
            var prev = lines[i - 1];
            var line = lines[i];
            var sizeChange = Math.Abs(line.Size - prev.Size) > Math.Max(prev.Size, 1) * 0.15;
            var wideGap = gaps[i - 1] > Math.Max(typicalGap * 1.6, line.Size * 0.5);
            if (sizeChange || wideGap || ListMarker(line.Text) is not null || line.Bold != prev.Bold)
            {
                AddParagraph(doc, current, body);
                current = new List<Line>();
            }
            current.Add(line);
        }
        AddParagraph(doc, current, body);
    }

    private static void AddParagraph(Document doc, List<Line> lines, double body)
    {
        var text = JoinLines(lines.Select(l => l.Text));
        // A bare page number is page furniture, not content.
        if (text.Length == 0 || (text.Length <= 4 && text.All(char.IsDigit)))
            return;
        var size = Median(lines.Select(l => l.Size).ToList());
        var bold = lines.All(l => l.Bold);

        if (ListMarker(text) is { } marker)
        {
            var ordered = char.IsDigit(marker[0]);
            var item = new Paragraph();
            item.Runs.Add(new Run(text[marker.Length..].TrimStart(), bold ? new TextStyle { Bold = true } : TextStyle.Plain));
            if (doc.Blocks.LastOrDefault() is ListBlock list && list.Ordered == ordered)
                list.Items.Add([item]);
            else
                doc.Blocks.Add(new ListBlock { Ordered = ordered, Items = { new List<Block> { item } } });
            return;
        }

        if (body > 0 && size >= body * 1.25 && text.Length < 200)
        {
            var h = new Heading(size >= body * 1.7 ? 1 : 2);
            h.Runs.Add(new Run(text, TextStyle.Plain));
            doc.Blocks.Add(h);
            return;
        }
        if (bold && text.Length < 120 && !text.EndsWith('.'))
        {
            var h = new Heading(3);
            h.Runs.Add(new Run(text, TextStyle.Plain));
            doc.Blocks.Add(h);
            return;
        }
        var p = new Paragraph();
        p.Runs.Add(new Run(text, bold ? new TextStyle { Bold = true } : TextStyle.Plain));
        doc.Blocks.Add(p);
    }

    /// <summary>The list marker a line starts with, such as a bullet, "1." or "a)", or null.</summary>
    public static string? ListMarker(string text)
    {
        if (text.Length < 2)
            return null;
        if ("\u2022\u25E6\u25AA\u25CF\u2013\u2023\u2043\u00B7".Contains(text[0]) && char.IsWhiteSpace(text[1]))
            return text[..1];
        var m = System.Text.RegularExpressions.Regex.Match(text, @"^(\d{1,3}|[a-z])[.)]\s");
        return m.Success ? m.Value.TrimEnd() : null;
    }

    /// <summary>Joins the lines of a block into one paragraph, rejoining words hyphenated at a line end.</summary>
    public static string JoinLines(IEnumerable<string> lines)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (sb.Length > 0)
            {
                var last = sb[^1];
                if (last is '-' or '­' && sb.Length > 1 && char.IsLetter(sb[^2]) && char.IsLower(line[0]))
                    sb.Length--;
                else
                    sb.Append(' ');
            }
            sb.Append(line);
        }
        return sb.ToString();
    }

    /// <summary>Median font size from a histogram in half point buckets.</summary>
    private static double HistogramMedian(Dictionary<int, long> histogram)
    {
        var total = histogram.Values.Sum();
        var seen = 0L;
        foreach (var (bucket, count) in histogram.OrderBy(h => h.Key))
        {
            seen += count;
            if (seen * 2 >= total)
                return bucket / 2.0;
        }
        return 0;
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
            return 0;
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }
}
