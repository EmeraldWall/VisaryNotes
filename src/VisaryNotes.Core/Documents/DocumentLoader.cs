using VisaryNotes.Core.Rendering;

namespace VisaryNotes.Core.Documents;

public enum DocumentKind
{
    Markdown,
    PlainText,
    Rtf,
    Odt,
    Unsupported,
    // Stored in the library database as numbers, so new kinds go at the end.
    Docx,
    Doc,
    Pdf
}

/// <summary>A document opened for viewing, ready to render in either view mode.</summary>
public sealed class LoadedDocument
{
    public required string FileName { get; init; }
    public required DocumentKind Kind { get; init; }
    public Document? Model { get; init; }
    public string? Markdown { get; init; }

    public string Title => Kind == DocumentKind.Markdown
        ? MarkdownRenderer.TitleOf(Markdown ?? "")
        : string.IsNullOrWhiteSpace(Model?.Title) ? FileName : Model!.Title;

    public string Html(ViewMode mode, ReaderSettings settings, IReadOnlyCollection<string>? highlights = null)
    {
        if (Kind == DocumentKind.Markdown)
            return HtmlRenderer.Page(MarkdownRenderer.ToHtml(Markdown ?? "", null, settings.AllowRemoteImages), ViewMode.Reading, settings, null, highlights);
        return HtmlRenderer.Render(Model!, mode, settings, highlights);
    }

    public string PlainText() => Kind == DocumentKind.Markdown ? MarkdownRenderer.ToPlainText(Markdown ?? "") : Model?.PlainText() ?? "";
}

/// <summary>Chooses a reader by file extension and MIME type (docs/SPEC.md 7.2).</summary>
public static class DocumentLoader
{
    public static DocumentKind KindOf(string fileName, string? mimeType = null)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".md" or ".markdown" or ".mdown" or ".mkd" => DocumentKind.Markdown,
            ".txt" or ".text" or ".log" => DocumentKind.PlainText,
            ".rtf" => DocumentKind.Rtf,
            ".odt" or ".fodt" => DocumentKind.Odt,
            ".docx" or ".docm" or ".dotx" => DocumentKind.Docx,
            ".doc" or ".dot" => DocumentKind.Doc,
            ".pdf" => DocumentKind.Pdf,
            _ => mimeType switch
            {
                "text/markdown" or "text/x-markdown" => DocumentKind.Markdown,
                "application/rtf" or "text/rtf" => DocumentKind.Rtf,
                "application/vnd.oasis.opendocument.text" => DocumentKind.Odt,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => DocumentKind.Docx,
                "application/msword" => DocumentKind.Doc,
                "application/pdf" => DocumentKind.Pdf,
                "text/plain" => DocumentKind.PlainText,
                _ => DocumentKind.Unsupported
            }
        };
    }

    public static LoadedDocument Load(Stream stream, string fileName, string? mimeType = null)
    {
        var bytes = ReadLimits.ReadAll(stream);
        var kind = Sniff(bytes, KindOf(fileName, mimeType));
        stream = new MemoryStream(bytes);
        return kind switch
        {
            DocumentKind.Markdown => new LoadedDocument
            {
                FileName = fileName,
                Kind = kind,
                Markdown = PlainTextReader.Decode(ReadLimits.ReadAll(stream)).Text
            },
            DocumentKind.PlainText => new LoadedDocument { FileName = fileName, Kind = kind, Model = PlainTextReader.Read(stream, fileName) },
            DocumentKind.Rtf => new LoadedDocument { FileName = fileName, Kind = kind, Model = RtfReader.Read(stream, fileName) },
            DocumentKind.Odt => new LoadedDocument { FileName = fileName, Kind = kind, Model = OdtReader.Read(stream, fileName) },
            DocumentKind.Docx => new LoadedDocument { FileName = fileName, Kind = kind, Model = DocxReader.Read(stream, fileName) },
            DocumentKind.Doc => new LoadedDocument { FileName = fileName, Kind = kind, Model = DocReader.Read(stream, fileName) },
            DocumentKind.Pdf => new LoadedDocument { FileName = fileName, Kind = kind, Model = PdfReader.Read(stream, fileName) },
            _ => throw new DocumentReadException($"{Path.GetExtension(fileName)} files are not supported yet.")
        };
    }

    /// <summary>
    /// Word files are often saved with the wrong extension: a .doc that is really .docx or RTF, or the
    /// reverse. The first bytes decide for the Word family; other kinds keep their extension.
    /// </summary>
    public static DocumentKind Sniff(byte[] bytes, DocumentKind byName)
    {
        if (byName is not (DocumentKind.Doc or DocumentKind.Docx or DocumentKind.Unsupported) || bytes.Length < 8)
            return byName;
        if (bytes[0] == 'P' && bytes[1] == 'K')
            return byName == DocumentKind.Unsupported ? byName : DocumentKind.Docx;
        if (bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0)
            return DocumentKind.Doc;
        if (bytes[0] == '{' && bytes[1] == '\\' && bytes[2] == 'r' && bytes[3] == 't' && bytes[4] == 'f')
            return DocumentKind.Rtf;
        if (bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F')
            return DocumentKind.Pdf;
        return byName;
    }

    /// <summary>True for formats whose bytes are not text, so the plain text fallback would show garbage.</summary>
    public static bool IsBinary(DocumentKind kind) => kind is DocumentKind.Doc or DocumentKind.Docx or DocumentKind.Pdf or DocumentKind.Odt;

    /// <summary>The fallback the viewer offers when a file cannot be parsed: show it as plain text.</summary>
    public static LoadedDocument AsPlainText(byte[] bytes, string fileName) => new()
    {
        FileName = fileName,
        Kind = DocumentKind.PlainText,
        Model = PlainTextReader.Read(new MemoryStream(bytes), fileName)
    };

    public static string Badge(DocumentKind kind) => kind switch
    {
        DocumentKind.Markdown => "MD",
        DocumentKind.PlainText => "TXT",
        DocumentKind.Rtf => "RTF",
        DocumentKind.Odt => "ODT",
        DocumentKind.Docx => "DOCX",
        DocumentKind.Doc => "DOC",
        DocumentKind.Pdf => "PDF",
        _ => "?"
    };
}
