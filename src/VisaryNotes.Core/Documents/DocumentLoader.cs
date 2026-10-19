using VisaryNotes.Core.Rendering;

namespace VisaryNotes.Core.Documents;

public enum DocumentKind
{
    Markdown,
    PlainText,
    Rtf,
    Odt,
    Unsupported
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
            _ => mimeType switch
            {
                "text/markdown" or "text/x-markdown" => DocumentKind.Markdown,
                "application/rtf" or "text/rtf" => DocumentKind.Rtf,
                "application/vnd.oasis.opendocument.text" => DocumentKind.Odt,
                "text/plain" => DocumentKind.PlainText,
                _ => DocumentKind.Unsupported
            }
        };
    }

    public static LoadedDocument Load(Stream stream, string fileName, string? mimeType = null)
    {
        var kind = KindOf(fileName, mimeType);
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
            _ => throw new DocumentReadException($"{Path.GetExtension(fileName)} files are not supported yet.")
        };
    }

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
        _ => "?"
    };
}
