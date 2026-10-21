using VisaryNotes.Core.Data;
using VisaryNotes.Core.Documents;
using VisaryNotes.Core.Export;

namespace VisaryNotes.Core.Tests;

public class DocxWriterTests
{
    private const string Note = """
        # Trip plan

        Bring **boots**, *maps* and ~~stoves~~ and `rope`. See [the guide](https://example.com/guide).

        - Day one
          - Pack
        - Day two

        1. Book
        2. Go

        - [x] Tickets

        > Leave early.

        | Item | Cost |
        | --- | --- |
        | Bus | 12 |
        """;

    private readonly Document _back = DocxReader.Read(new MemoryStream(DocxWriter.FromMarkdown(Note, "Trip plan")), "Trip plan.docx");

    [Fact]
    public void Round_trips_title_headings_and_formatting()
    {
        Assert.Equal("Trip plan", _back.Title);
        Assert.Equal(("Trip plan", 1), (_back.Blocks.OfType<Heading>().First().Text, _back.Blocks.OfType<Heading>().First().Level));
        var p = _back.Blocks.OfType<Paragraph>().First();
        Assert.Contains(p.Runs, r => r.Text == "boots" && r.Style.Bold);
        Assert.Contains(p.Runs, r => r.Text == "maps" && r.Style.Italic);
        Assert.Contains(p.Runs, r => r.Text == "stoves" && r.Style.Strike);
        Assert.Contains(p.Runs, r => r.Text == "the guide" && r.Link == "https://example.com/guide");
    }

    [Fact]
    public void Round_trips_nested_and_numbered_lists_and_tasks()
    {
        var lists = _back.Blocks.OfType<ListBlock>().ToList();
        Assert.False(lists[0].Ordered);
        Assert.Equal(2, lists[0].Items.Count);
        Assert.Single(lists[0].Items[0].OfType<ListBlock>());
        Assert.Contains(lists, l => l.Ordered && l.Items.Count == 2);
        Assert.Contains("☑ Tickets", _back.PlainText());
    }

    [Fact]
    public void Round_trips_tables_and_escapes_xml()
    {
        var table = _back.Blocks.OfType<TableBlock>().Single();
        Assert.Equal("Bus", table.Rows[1][0].Blocks.OfType<Paragraph>().Single().Text);
        var tricky = DocxReader.Read(new MemoryStream(DocxWriter.FromMarkdown("A < B & C > D \"quoted\"", "x")), "x.docx");
        Assert.Equal("A < B & C > D \"quoted\"", tricky.Blocks.OfType<Paragraph>().Single().Text);
    }
}

public class RemoveFromLibraryTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"vn-{Guid.NewGuid():N}.db3");
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
    public async Task Keeps_highlights_and_quotes_and_undo_restores_the_entry()
    {
        var doc = await _repo.RecordOpenAsync("/x/report.odt", "report.odt", DocumentKind.Odt, addToLibrary: true);
        await _repo.AddHighlightAsync(doc.Id, "key finding");
        await _repo.QuoteToNoteAsync(doc.Id, "report.odt", "key finding");

        var highlights = (await _repo.GetHighlightsAsync(doc.Id)).Count;

        await _repo.RemoveFromLibraryAsync(doc.Id);
        Assert.DoesNotContain(await _repo.GetLibraryAsync(LibraryFilter.Documents), i => i.Id == doc.Id);
        Assert.Equal(highlights, (await _repo.GetHighlightsAsync(doc.Id)).Count);
        Assert.NotNull(await _repo.GetLinkedNoteAsync(doc.Id));

        await _repo.SaveToLibraryAsync(doc.Id);
        Assert.Contains(await _repo.GetLibraryAsync(LibraryFilter.Documents), i => i.Id == doc.Id);
    }
}

public class TrashAndCleanUpTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"vn-{Guid.NewGuid():N}.db3");
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
    public async Task Trash_lists_deleted_notes_with_days_left_and_can_be_emptied()
    {
        var keep = await _repo.SaveNoteAsync(new Note { Body = "# Keep" });
        var gone = await _repo.SaveNoteAsync(new Note { Body = "# Gone" });
        await _repo.MoveToTrashAsync(gone.Id);

        var trash = await _repo.GetLibraryAsync(LibraryFilter.Trash);
        var item = Assert.Single(trash);
        Assert.Equal("Gone", item.Title);
        Assert.Equal("30 days left", item.Snippet);
        Assert.DoesNotContain(await _repo.GetLibraryAsync(LibraryFilter.All), i => i.Id == gone.Id);

        Assert.Equal(1, await _repo.EmptyTrashAsync());
        Assert.Empty(await _repo.GetLibraryAsync(LibraryFilter.Trash));
        Assert.Null(await _repo.GetNoteAsync(gone.Id));
        Assert.NotNull(await _repo.GetNoteAsync(keep.Id));
        Assert.Empty(await _repo.SearchAsync("Gone"));
    }

    [Fact]
    public async Task Clean_up_hides_only_stale_documents_and_keeps_their_highlights()
    {
        var old = await _repo.RecordOpenAsync("/x/old.pdf", "old.pdf", DocumentKind.Pdf, addToLibrary: true);
        await Task.Delay(50);
        var fresh = await _repo.RecordOpenAsync("/x/new.pdf", "new.pdf", DocumentKind.Pdf, addToLibrary: true);
        await _repo.AddHighlightAsync(old.Id, "keep me");

        // Everything opened before the fresh document counts as stale.
        var removed = await _repo.CleanUpDocumentsAsync(fresh.LastOpenedAt);
        Assert.Contains(old.Id, removed);
        Assert.DoesNotContain(fresh.Id, removed);
        Assert.Single(await _repo.GetHighlightsAsync(old.Id));
    }
}

public class ReaderInsetTests
{
    [Fact]
    public void Page_reserves_the_bars_space_from_the_first_frame()
    {
        var html = Rendering.HtmlRenderer.Page("<main class=\"reading\"><h1>T</h1></main>", Rendering.ViewMode.Reading,
            new Rendering.ReaderSettings(Theming.ThemeCatalog.Get("Daylight"), TopInsetPx: 48, BottomInsetPx: 132));
        Assert.Contains("--vn-top:48px;--vn-bottom:132px", html);
        Assert.Contains("window.vnHeadings", html);
        Assert.Contains("send('ready')", html);
    }
}
