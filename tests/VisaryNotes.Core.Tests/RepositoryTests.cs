using VisaryNotes.Core.Data;
using VisaryNotes.Core.Documents;

namespace VisaryNotes.Core.Tests;

public sealed class RepositoryTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"visary-test-{Guid.NewGuid():N}.db");
    private LibraryRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _repo = new LibraryRepository(_path);
        await _repo.InitAsync();
    }

    public async Task DisposeAsync()
    {
        await _repo.CloseAsync();
        File.Delete(_path);
    }

    [Fact]
    public async Task Saving_a_note_derives_its_title()
    {
        var note = await _repo.SaveNoteAsync(new Note { Body = "# Garden plan\n\nRaised beds along the south fence." });
        Assert.Equal("Garden plan", note.Title);
        var items = await _repo.GetLibraryAsync(LibraryFilter.Notes);
        Assert.Equal("Raised beds along the south fence.", Assert.Single(items).Snippet);
    }

    [Fact]
    public async Task Search_uses_full_text_index_and_survives_odd_input()
    {
        await _repo.SaveNoteAsync(new Note { Body = "# Sprint retro\n\nFlaky upload test" });
        await _repo.SaveNoteAsync(new Note { Body = "# Groceries\n\nOat milk" });
        var hits = await _repo.SearchAsync("uplo");
        Assert.Equal("Sprint retro", Assert.Single(hits).Title);
        Assert.Empty(await _repo.SearchAsync("\" OR * NEAR("));
    }

    [Fact]
    public async Task Quote_to_note_creates_a_linked_note_and_a_highlight()
    {
        var doc = await _repo.RecordOpenAsync("content://x/newsletter.odt", "Autumn Newsletter.odt", DocumentKind.Odt, true);
        await _repo.QuoteToNoteAsync(doc.Id, doc.DisplayName, "Bring a basket and gloves.");
        var note = await _repo.QuoteToNoteAsync(doc.Id, doc.DisplayName, "Green waste goes in the left bay.");

        Assert.Equal("Notes on Autumn Newsletter", note.Title);
        Assert.Contains("> Bring a basket and gloves.", note.Body);
        Assert.Contains("> Green waste goes in the left bay.", note.Body);
        Assert.Contains($"(visary-doc://{doc.Id})", note.Body);
        Assert.Equal(2, (await _repo.GetHighlightsAsync(doc.Id)).Count);

        var linked = await _repo.GetLibraryAsync(LibraryFilter.Linked);
        Assert.Equal(2, linked.Count);
        Assert.Contains(linked, i => i.IsDocument && i.Snippet.Contains("2 highlights"));
    }

    [Fact]
    public async Task Files_opened_from_other_apps_stay_out_of_the_library_until_saved()
    {
        var doc = await _repo.RecordOpenAsync("content://mail/lease.rtf", "Lease renewal.rtf", DocumentKind.Rtf, false);
        Assert.Empty(await _repo.GetLibraryAsync(LibraryFilter.Documents));
        await _repo.SaveToLibraryAsync(doc.Id);
        Assert.Single(await _repo.GetLibraryAsync(LibraryFilter.Documents));
    }

    [Fact]
    public async Task Continue_reading_returns_unfinished_document()
    {
        var doc = await _repo.RecordOpenAsync("content://a.odt", "a.odt", DocumentKind.Odt, true);
        await _repo.SetProgressAsync(doc.Id, 0.4, 0);
        Assert.Equal(doc.Id, (await _repo.GetContinueReadingAsync())!.Id);
        await _repo.SetProgressAsync(doc.Id, 1, 0);
        Assert.Null(await _repo.GetContinueReadingAsync());
    }

    [Fact]
    public async Task Trash_hides_and_restore_brings_back()
    {
        var note = await _repo.SaveNoteAsync(new Note { Body = "# Temp" });
        await _repo.MoveToTrashAsync(note.Id);
        Assert.Empty(await _repo.GetLibraryAsync(LibraryFilter.All));
        Assert.Empty(await _repo.SearchAsync("Temp"));
        await _repo.RestoreAsync(note.Id);
        Assert.Single(await _repo.GetLibraryAsync(LibraryFilter.All));
    }

    [Fact]
    public async Task Imports_markdown_files()
    {
        var count = await _repo.ImportMarkdownAsync([("Lisbon.md", "Day 1 Alfama"), ("Retro.md", "# Retro\n\nText")]);
        Assert.Equal(2, count);
        var titles = (await _repo.GetLibraryAsync(LibraryFilter.Notes)).Select(i => i.Title).ToHashSet();
        Assert.Contains("Lisbon", titles);
        Assert.Contains("Retro", titles);
    }
}
