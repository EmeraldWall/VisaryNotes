using System.Security.Cryptography;
using VisaryNotes.Core.Data;
using VisaryNotes.Core.Documents;

namespace VisaryNotes.App.Services;

public sealed record OpenedDocument(DocumentEntry Entry, LoadedDocument Document, string? Warning);

/// <summary>
/// Opens documents from the file picker or from other apps (docs/SPEC.md 7.1 and 7.7).
/// Each opened file is copied into app storage so it can be reopened from the Library later.
/// </summary>
public sealed class DocumentService
{
    private static readonly FilePickerFileType SupportedTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] =
        [
            "text/markdown", "text/x-markdown", "text/plain", "application/rtf", "text/rtf",
            "application/vnd.oasis.opendocument.text", "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"
        ]
    });

    private readonly LibraryRepository _repo;
    // The document just imported, so the reader that opens next does not parse the file a second time.
    private OpenedDocument? _justImported;
    private readonly string _docsDir = Path.Combine(FileSystem.AppDataDirectory, "documents");

    public DocumentService(LibraryRepository repo)
    {
        _repo = repo;
        Directory.CreateDirectory(_docsDir);
    }

    public async Task<OpenedDocument?> PickAndOpenAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Open a document", FileTypes = SupportedTypes });
        if (result is null)
            return null;
        await using var stream = await result.OpenReadAsync();
        return await ImportAsync(stream, result.FileName, addToLibrary: true);
    }

    /// <summary>
    /// Bring Markdown notes from another app, used by the Library's first run and by Settings. Returns how many
    /// notes were added, 0 when the user picked nothing.
    /// </summary>
    public async Task<int> ImportMarkdownAsync()
    {
        var files = await PickMarkdownFilesAsync();
        return files.Count == 0 ? 0 : await _repo.ImportMarkdownAsync(files);
    }

    public static string ImportedText(int count) => count == 1 ? "Imported 1 note" : $"Imported {count} notes";

    private async Task<IReadOnlyList<(string Name, string Text)>> PickMarkdownFilesAsync()
    {
        var results = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Choose Markdown files to import", FileTypes = SupportedTypes });
        var files = new List<(string, string)>();
        foreach (var r in results ?? [])
        {
            if (r is null || DocumentLoader.KindOf(r.FileName) != DocumentKind.Markdown)
                continue;
            await using var s = await r.OpenReadAsync();
            files.Add((r.FileName, PlainTextReader.Decode(ReadLimits.ReadAll(s)).Text));
        }
        return files;
    }

    /// <summary>A name with the characters files cannot hold removed, or the fallback when nothing is left.</summary>
    public static string SafeFileName(string name, string fallback)
    {
        var safe = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
        return safe.Length > 0 ? safe : fallback;
    }

    /// <summary>Stores a private copy, records the document, and parses it off the UI thread.</summary>
    public async Task<OpenedDocument> ImportAsync(Stream stream, string fileName, bool addToLibrary)
    {
        var bytes = await Task.Run(() => ReadLimits.ReadAll(stream));
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
        var safeName = SafeFileName(fileName, "Document");
        var path = Path.Combine(_docsDir, $"{hash}-{safeName}");
        if (!File.Exists(path))
            await File.WriteAllBytesAsync(path, bytes);

        var kind = DocumentLoader.KindOf(fileName);
        var entry = await _repo.RecordOpenAsync(path, fileName, kind, addToLibrary);
        var (doc, warning) = await Task.Run(() => Load(bytes, fileName));
        _justImported = new OpenedDocument(entry, doc, warning);
        return _justImported;
    }

    public async Task<OpenedDocument?> OpenEntryAsync(int documentId)
    {
        if (_justImported is { } recent && recent.Entry.Id == documentId)
        {
            _justImported = null;
            return recent;
        }
        var entry = await _repo.GetDocumentAsync(documentId);
        if (entry is null || !File.Exists(entry.Uri))
            return null;
        entry = await _repo.RecordOpenAsync(entry.Uri, entry.DisplayName, entry.Kind, entry.InLibrary);
        var bytes = await File.ReadAllBytesAsync(entry.Uri);
        var (doc, warning) = await Task.Run(() => Load(bytes, entry.DisplayName));
        return new OpenedDocument(entry, doc, warning);
    }

    private static (LoadedDocument, string?) Load(byte[] bytes, string fileName)
    {
        try
        {
            return (DocumentLoader.Load(new MemoryStream(bytes), fileName), null);
        }
        catch (Exception ex) when (ex is DocumentReadException or System.Xml.XmlException or InvalidDataException)
        {
            var kind = DocumentLoader.Sniff(bytes, DocumentLoader.KindOf(fileName));
            if (DocumentLoader.IsBinary(kind))
            {
                // Binary formats would show as garbage text, so show an empty page with the reason instead.
                var empty = new LoadedDocument { FileName = fileName, Kind = kind, Model = new Document { Title = Path.GetFileNameWithoutExtension(fileName) } };
                return (empty, $"This {DocumentLoader.Badge(kind)} file could not be opened. {ex.Message}");
            }
            // Never crash on a bad file: show it as plain text with a short explanation (docs/SPEC.md 7.9).
            return (DocumentLoader.AsPlainText(bytes, fileName), $"This file could not be read as {DocumentLoader.Badge(DocumentLoader.KindOf(fileName))}, so it is shown as plain text. {ex.Message}");
        }
    }
}
