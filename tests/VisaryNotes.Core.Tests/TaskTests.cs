using VisaryNotes.Core.Data;

namespace VisaryNotes.Core.Tests;

public class NoteTasksTests
{
    private const string Body = """
        # Trip

        - [ ] Book tickets (@2026-10-23 14:00)
        - [x] Pack
        * [ ] Call Sam (@2026-10-24)
        - plain item

        ```
        - [ ] not a task inside code
        ```
        """;

    [Fact]
    public void Parses_tasks_with_times_and_whole_days_and_skips_code()
    {
        var tasks = NoteTasks.Parse(Body);
        Assert.Equal(3, tasks.Count);
        Assert.Equal(new NoteTask(2, "Book tickets", false, new DateTime(2026, 10, 23, 14, 0, 0), true), tasks[0]);
        Assert.True(tasks[1].Done);
        Assert.Equal(new DateTime(2026, 10, 24, NoteTasks.DayReminderHour, 0, 0), NoteTasks.ReminderAt(tasks[2]));
    }

    [Fact]
    public void Task_boxes_match_the_rendered_page_one_to_one()
    {
        const string md = "- [ ] a\n1. [x] b\n- [ ]c\n- [ ]\n\n```\n- [ ] code\n```\n* [ ] d";
        var html = Rendering.MarkdownRenderer.ToHtml(md);
        var boxes = html.Split("type=\"checkbox\"").Length - 1;
        Assert.Equal(boxes, NoteTasks.Parse(md).Count);
    }

    [Fact]
    public void Toggle_ticks_and_unticks_one_line_only()
    {
        var ticked = NoteTasks.Toggle(Body, 2);
        Assert.Contains("- [x] Book tickets (@2026-10-23 14:00)", ticked);
        Assert.Equal(Body.Replace("\r\n", "\n"), NoteTasks.Toggle(ticked, 2));
    }

    [Fact]
    public void SetDue_adds_replaces_and_clears_the_time_and_turns_a_line_into_a_task()
    {
        var at = new DateTime(2026, 11, 1, 8, 30, 0);
        Assert.Contains("- [ ] Book tickets (@2026-11-01 08:30)", NoteTasks.SetDue(Body, 2, at));
        Assert.Contains("- [ ] Book tickets\n", NoteTasks.SetDue(Body, 2, null));
        Assert.Contains("- [ ] plain item (@2026-11-01 08:30)", NoteTasks.SetDue(Body, 5, at));
        Assert.Equal("  - [ ] Buy milk (@2026-11-01)", NoteTasks.SetDue("  Buy milk", 0, at, hasTime: false));
    }

    [Fact]
    public void Rename_replaces_the_heading_or_adds_one()
    {
        Assert.Equal("## Holiday\n\ntext", NoteTitles.Rename("## Trip\n\ntext", "Holiday"));
        Assert.Equal("# Holiday\n\nJust text", NoteTitles.Rename("Just text", "Holiday"));
        Assert.Equal("# Trip", NoteTitles.Rename("# Trip", "   "));
    }
}

public class TaskRepositoryTests : IAsyncLifetime
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
    public async Task Tasks_list_timed_first_and_reminders_come_due_in_order()
    {
        var a = await _repo.SaveNoteAsync(new Note { Body = "# A\n\n- [ ] Later (@2026-12-01 10:00)\n- [ ] Someday\n- [x] Done (@2026-11-01 10:00)" });
        var b = await _repo.SaveNoteAsync(new Note { Body = "# B\n\n- [ ] Soon (@2026-11-02 09:15)" });

        var tasks = await _repo.GetLibraryAsync(LibraryFilter.Tasks);
        Assert.Equal(["Soon", "Later", "Someday"], tasks.Select(t => t.Title));
        Assert.All(tasks, t => Assert.True(t.IsTask));
        Assert.Equal("B", tasks[0].Snippet);

        Assert.Equal(new DateTime(2026, 11, 2, 9, 15, 0), await _repo.NextReminderAsync(new DateTime(2026, 11, 1)));
        var due = await _repo.RemindersBetweenAsync(new DateTime(2026, 11, 2), new DateTime(2026, 11, 2, 9, 15, 0));
        Assert.Equal(b.Id, Assert.Single(due).NoteId);

        await _repo.ToggleTaskAsync(b.Id, 2);
        Assert.Equal(new DateTime(2026, 12, 1, 10, 0, 0), await _repo.NextReminderAsync(new DateTime(2026, 11, 1)));
        await _repo.MoveToTrashAsync(a.Id);
        Assert.Null(await _repo.NextReminderAsync(new DateTime(2026, 11, 1)));
    }

    [Fact]
    public async Task Quick_tasks_go_into_one_Tasks_note()
    {
        await _repo.AddTaskAsync("Buy milk", null);
        var note = await _repo.AddTaskAsync("Call Sam", new DateTime(2026, 11, 3, 9, 0, 0));
        Assert.Equal("# Tasks\n\n- [ ] Buy milk\n- [ ] Call Sam (@2026-11-03 09:00)\n", note.Body);
        Assert.Single(await _repo.GetLibraryAsync(LibraryFilter.Notes));
    }

    [Fact]
    public async Task Renaming_a_document_keeps_its_extension()
    {
        var doc = await _repo.RecordOpenAsync("/x/report.odt", "report.odt", Documents.DocumentKind.Odt, addToLibrary: true);
        await _repo.RenameDocumentAsync(doc.Id, "Board report");
        Assert.Contains(await _repo.GetLibraryAsync(LibraryFilter.Documents), i => i.Title == "Board report.odt");
        var note = await _repo.SaveNoteAsync(new Note { Body = "# Old\n\nx" });
        Assert.Equal("New", (await _repo.RenameNoteAsync(note.Id, "New"))!.Title);
    }
}
