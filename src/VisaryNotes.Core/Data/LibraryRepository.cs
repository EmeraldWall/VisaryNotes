using System.Text;
using SQLite;
using VisaryNotes.Core.Documents;
using VisaryNotes.Core.Rendering;

namespace VisaryNotes.Core.Data;

/// <summary>All Library storage: notes, opened documents, highlights and the links between them (docs/SPEC.md 8.1, 11.3).</summary>
public sealed class LibraryRepository
{
    public const string DocumentLinkScheme = "visary-doc://";
    private static readonly TimeSpan TrashRetention = TimeSpan.FromDays(30);

    private readonly SQLiteAsyncConnection _db;
    private bool _ready;

    public LibraryRepository(string databasePath)
    {
        _db = new SQLiteAsyncConnection(databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public async Task InitAsync()
    {
        if (_ready)
            return;
        await _db.CreateTablesAsync<Note, DocumentEntry, Highlight, NoteLink>().ConfigureAwait(false);
        await _db.ExecuteAsync("CREATE VIRTUAL TABLE IF NOT EXISTS note_search USING fts5(title, body)").ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM notes WHERE DeletedAt IS NOT NULL AND DeletedAt < ?", DateTime.UtcNow - TrashRetention).ConfigureAwait(false);
        _ready = true;
    }

    // Notes

    public async Task<Note?> GetNoteAsync(int id)
    {
        await InitAsync().ConfigureAwait(false);
        return await _db.FindAsync<Note>(id).ConfigureAwait(false);
    }

    public async Task<Note> SaveNoteAsync(Note note)
    {
        await InitAsync().ConfigureAwait(false);
        note.Title = MarkdownRenderer.TitleOf(note.Body);
        note.UpdatedAt = DateTime.UtcNow;
        if (note.Id == 0)
            await _db.InsertAsync(note).ConfigureAwait(false);
        else
            await _db.UpdateAsync(note).ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM note_search WHERE rowid = ?", note.Id).ConfigureAwait(false);
        await _db.ExecuteAsync("INSERT INTO note_search(rowid, title, body) VALUES (?, ?, ?)", note.Id, note.Title, note.Body).ConfigureAwait(false);
        return note;
    }

    public async Task MoveToTrashAsync(int noteId)
    {
        var note = await _db.FindAsync<Note>(noteId).ConfigureAwait(false);
        if (note is null)
            return;
        note.DeletedAt = DateTime.UtcNow;
        await _db.UpdateAsync(note).ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM note_search WHERE rowid = ?", noteId).ConfigureAwait(false);
    }

    /// <summary>Permanently deletes a note in the Trash, with its search entry and links.</summary>
    public async Task DeleteForeverAsync(int noteId)
    {
        await InitAsync().ConfigureAwait(false);
        await _db.DeleteAsync<Note>(noteId).ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM note_search WHERE rowid = ?", noteId).ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM note_links WHERE NoteId = ?", noteId).ConfigureAwait(false);
    }

    /// <summary>Permanently deletes every note in the Trash. Returns how many were deleted.</summary>
    public async Task<int> EmptyTrashAsync()
    {
        await InitAsync().ConfigureAwait(false);
        var ids = (await _db.Table<Note>().Where(n => n.DeletedAt != null).ToListAsync().ConfigureAwait(false)).Select(n => n.Id).ToList();
        foreach (var id in ids)
            await DeleteForeverAsync(id).ConfigureAwait(false);
        return ids.Count;
    }

    /// <summary>
    /// Takes documents not opened since <paramref name="before"/> off the Library list. Like a single removal,
    /// highlights and quotes are kept. Returns the ids so the caller can offer Undo.
    /// </summary>
    public async Task<IReadOnlyList<int>> CleanUpDocumentsAsync(DateTime before)
    {
        await InitAsync().ConfigureAwait(false);
        var stale = await _db.Table<DocumentEntry>().Where(d => d.InLibrary && d.LastOpenedAt < before).ToListAsync().ConfigureAwait(false);
        foreach (var d in stale)
        {
            d.InLibrary = false;
            await _db.UpdateAsync(d).ConfigureAwait(false);
        }
        return stale.Select(d => d.Id).ToList();
    }

    private static string TrashLeft(DateTime deletedUtc)
    {
        var days = Math.Max(0, (int)Math.Ceiling((deletedUtc + TrashRetention - DateTime.UtcNow).TotalDays));
        return days == 1 ? "1 day left" : $"{days} days left";
    }

    public async Task RestoreAsync(int noteId)
    {
        var note = await _db.FindAsync<Note>(noteId).ConfigureAwait(false);
        if (note is null)
            return;
        note.DeletedAt = null;
        await SaveNoteAsync(note).ConfigureAwait(false);
    }

    public async Task SetPinnedAsync(int noteId, bool pinned)
    {
        var note = await _db.FindAsync<Note>(noteId).ConfigureAwait(false);
        if (note is null)
            return;
        note.Pinned = pinned;
        await _db.UpdateAsync(note).ConfigureAwait(false);
    }

    /// <summary>Imports Markdown files, for example a Markor or Obsidian folder (docs/SPEC.md 6.1, STRATEGY.md D6).</summary>
    public async Task<int> ImportMarkdownAsync(IEnumerable<(string FileName, string Text)> files)
    {
        var count = 0;
        foreach (var (name, text) in files)
        {
            var body = text.TrimStart().StartsWith('#') ? text : $"# {Path.GetFileNameWithoutExtension(name)}\n\n{text}";
            await SaveNoteAsync(new Note { Body = body }).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    // Documents

    public async Task<DocumentEntry> RecordOpenAsync(string uri, string displayName, DocumentKind kind, bool addToLibrary)
    {
        await InitAsync().ConfigureAwait(false);
        var entry = await _db.Table<DocumentEntry>().Where(d => d.Uri == uri).FirstOrDefaultAsync().ConfigureAwait(false);
        if (entry is null)
        {
            entry = new DocumentEntry { Uri = uri, DisplayName = displayName, Kind = kind, InLibrary = addToLibrary };
            await _db.InsertAsync(entry).ConfigureAwait(false);
        }
        else
        {
            entry.LastOpenedAt = DateTime.UtcNow;
            entry.DisplayName = displayName;
            entry.InLibrary |= addToLibrary;
            await _db.UpdateAsync(entry).ConfigureAwait(false);
        }
        return entry;
    }

    public async Task<DocumentEntry?> GetDocumentAsync(int id)
    {
        await InitAsync().ConfigureAwait(false);
        return await _db.FindAsync<DocumentEntry>(id).ConfigureAwait(false);
    }

    public async Task SaveToLibraryAsync(int documentId)
    {
        var d = await _db.FindAsync<DocumentEntry>(documentId).ConfigureAwait(false);
        if (d is null || d.InLibrary)
            return;
        d.InLibrary = true;
        await _db.UpdateAsync(d).ConfigureAwait(false);
    }

    public async Task SetProgressAsync(int documentId, double progress, int viewMode)
    {
        var d = await _db.FindAsync<DocumentEntry>(documentId).ConfigureAwait(false);
        if (d is null)
            return;
        d.Progress = Math.Clamp(progress, 0, 1);
        d.ViewMode = viewMode;
        await _db.UpdateAsync(d).ConfigureAwait(false);
    }

    /// <summary>The most recently opened Library document that is not finished.</summary>
    public async Task<DocumentEntry?> GetContinueReadingAsync()
    {
        await InitAsync().ConfigureAwait(false);
        return await _db.Table<DocumentEntry>()
            .Where(d => d.InLibrary && d.Progress < 0.98)
            .OrderByDescending(d => d.LastOpenedAt)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Takes a document off the Library list but keeps its highlights and note links, so Undo, or opening
    /// the same file again, brings everything back. Nothing the user made is deleted.
    /// </summary>
    public async Task RemoveFromLibraryAsync(int documentId)
    {
        await InitAsync().ConfigureAwait(false);
        var d = await _db.FindAsync<DocumentEntry>(documentId).ConfigureAwait(false);
        if (d is null || !d.InLibrary)
            return;
        d.InLibrary = false;
        await _db.UpdateAsync(d).ConfigureAwait(false);
    }

    /// <summary>Deletes a document record with its highlights and links. Not offered in the UI.</summary>
    public async Task RemoveDocumentAsync(int documentId)
    {
        await _db.DeleteAsync<DocumentEntry>(documentId).ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM highlights WHERE DocumentId = ?", documentId).ConfigureAwait(false);
        await _db.ExecuteAsync("DELETE FROM note_links WHERE DocumentId = ?", documentId).ConfigureAwait(false);
    }

    // Highlights and the read, extract, write loop (docs/SPEC.md 7.8)

    public async Task<IReadOnlyList<string>> GetHighlightsAsync(int documentId)
    {
        await InitAsync().ConfigureAwait(false);
        var list = await _db.Table<Highlight>().Where(h => h.DocumentId == documentId).ToListAsync().ConfigureAwait(false);
        return list.Select(h => h.Text).ToList();
    }

    public async Task AddHighlightAsync(int documentId, string text)
    {
        var clean = Normalise(text);
        if (clean.Length == 0)
            return;
        await _db.InsertAsync(new Highlight { DocumentId = documentId, Text = clean }).ConfigureAwait(false);
    }

    public async Task<Note?> GetLinkedNoteAsync(int documentId)
    {
        await InitAsync().ConfigureAwait(false);
        var link = await _db.Table<NoteLink>().Where(l => l.DocumentId == documentId && l.IsPrimary).FirstOrDefaultAsync().ConfigureAwait(false);
        if (link is null)
            return null;
        var note = await _db.FindAsync<Note>(link.NoteId).ConfigureAwait(false);
        return note is { DeletedAt: null } ? note : null;
    }

    /// <summary>
    /// Quotes a passage into the document's primary linked note, creating the note on first use.
    /// The quote is a Markdown blockquote followed by a source line that links back to the document.
    /// </summary>
    public async Task<Note> QuoteToNoteAsync(int documentId, string documentName, string text)
    {
        var clean = Normalise(text);
        var note = await GetOrCreateLinkedNoteAsync(documentId, documentName).ConfigureAwait(false);

        var sb = new StringBuilder(note.Body.TrimEnd('\n'));
        sb.Append("\n\n");
        foreach (var line in clean.Split('\n'))
            sb.Append("> ").Append(line).Append('\n');
        sb.Append("\n[").Append(documentName).Append("](").Append(DocumentLinkScheme).Append(documentId).Append(")\n");
        note.Body = sb.ToString();
        await AddHighlightAsync(documentId, clean).ConfigureAwait(false);
        return await SaveNoteAsync(note).ConfigureAwait(false);
    }

    /// <summary>The document's primary linked note, created on first use.</summary>
    public async Task<Note> GetOrCreateLinkedNoteAsync(int documentId, string documentName)
    {
        var note = await GetLinkedNoteAsync(documentId).ConfigureAwait(false);
        if (note is not null)
            return note;
        note = await SaveNoteAsync(new Note { Body = $"# Notes on {Path.GetFileNameWithoutExtension(documentName)}\n" }).ConfigureAwait(false);
        await _db.InsertAsync(new NoteLink { NoteId = note.Id, DocumentId = documentId, IsPrimary = true }).ConfigureAwait(false);
        return note;
    }

    private static string Normalise(string text) => string.Join('\n', text.Replace("\r", "").Split('\n').Select(l => l.Trim())).Trim();

    // Library list and search

    public async Task<IReadOnlyList<LibraryItem>> GetLibraryAsync(LibraryFilter filter)
    {
        await InitAsync().ConfigureAwait(false);
        var items = new List<LibraryItem>();
        if (filter == LibraryFilter.Trash)
        {
            var deleted = await _db.Table<Note>().Where(n => n.DeletedAt != null).ToListAsync().ConfigureAwait(false);
            return deleted
                .OrderByDescending(n => n.DeletedAt)
                .Select(n => new LibraryItem(LibraryItemKind.Note, n.Id, n.Title, TrashLeft(n.DeletedAt!.Value), "MD", n.DeletedAt!.Value, false, false))
                .ToList();
        }
        if (filter == LibraryFilter.Tasks)
            return await GetTasksAsync().ConfigureAwait(false);
        var linkedNotes = (await _db.Table<NoteLink>().ToListAsync().ConfigureAwait(false));
        var linkedNoteIds = linkedNotes.Select(l => l.NoteId).ToHashSet();
        var linkedDocIds = linkedNotes.Select(l => l.DocumentId).ToHashSet();

        if (filter is LibraryFilter.All or LibraryFilter.Notes or LibraryFilter.Linked)
        {
            var notes = await _db.Table<Note>().Where(n => n.DeletedAt == null && !n.Archived).ToListAsync().ConfigureAwait(false);
            items.AddRange(notes
                .Where(n => filter != LibraryFilter.Linked || linkedNoteIds.Contains(n.Id))
                .Select(n => new LibraryItem(LibraryItemKind.Note, n.Id, n.Title, Snippet(n.Body), "MD", n.UpdatedAt, linkedNoteIds.Contains(n.Id), n.Pinned)));
        }
        if (filter is LibraryFilter.All or LibraryFilter.Documents or LibraryFilter.Linked)
        {
            var docs = await _db.Table<DocumentEntry>().Where(d => d.InLibrary).ToListAsync().ConfigureAwait(false);
            var counts = (await _db.Table<Highlight>().ToListAsync().ConfigureAwait(false))
                .GroupBy(h => h.DocumentId).ToDictionary(g => g.Key, g => g.Count());
            items.AddRange(docs
                .Where(d => filter != LibraryFilter.Linked || linkedDocIds.Contains(d.Id))
                .Select(d => new LibraryItem(LibraryItemKind.Document, d.Id, d.DisplayName,
                    DocSnippet(d, counts.GetValueOrDefault(d.Id), linkedDocIds.Contains(d.Id)),
                    DocumentLoader.Badge(d.Kind), d.LastOpenedAt, linkedDocIds.Contains(d.Id), false)));
        }
        return items.OrderByDescending(i => i.Pinned).ThenByDescending(i => i.UpdatedAt).ToList();
    }

    public async Task<IReadOnlyList<LibraryItem>> SearchAsync(string query)
    {
        await InitAsync().ConfigureAwait(false);
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Replace("\"", ""))
            .Where(t => t.Length > 0)
            .ToList();
        if (terms.Count == 0)
            return await GetLibraryAsync(LibraryFilter.All).ConfigureAwait(false);

        // Each term is quoted so user input can never be parsed as FTS syntax, and prefix matched.
        var match = string.Join(" ", terms.Select(t => $"\"{t}\"*"));
        var ids = await _db.QueryScalarsAsync<int>("SELECT rowid FROM note_search WHERE note_search MATCH ? ORDER BY rank LIMIT 200", match).ConfigureAwait(false);
        var notes = new List<LibraryItem>();
        foreach (var id in ids)
        {
            var n = await _db.FindAsync<Note>(id).ConfigureAwait(false);
            if (n is { DeletedAt: null })
                notes.Add(new LibraryItem(LibraryItemKind.Note, n.Id, n.Title, Snippet(n.Body), "MD", n.UpdatedAt, false, n.Pinned));
        }
        var docs = await _db.Table<DocumentEntry>().Where(d => d.InLibrary).ToListAsync().ConfigureAwait(false);
        notes.AddRange(docs
            .Where(d => terms.All(t => d.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Select(d => new LibraryItem(LibraryItemKind.Document, d.Id, d.DisplayName, DocumentLoader.Badge(d.Kind) + " document", DocumentLoader.Badge(d.Kind), d.LastOpenedAt, false, false)));
        return notes;
    }

    public async Task<(int Notes, int Documents)> CountsAsync()
    {
        await InitAsync().ConfigureAwait(false);
        var n = await _db.Table<Note>().Where(x => x.DeletedAt == null && !x.Archived).CountAsync().ConfigureAwait(false);
        var d = await _db.Table<DocumentEntry>().Where(x => x.InLibrary).CountAsync().ConfigureAwait(false);
        return (n, d);
    }

    public Task CloseAsync() => _db.CloseAsync();

    // Tasks and reminders (docs/SPEC.md 6.4)

    private async Task<List<Note>> LiveNotesAsync() =>
        await _db.Table<Note>().Where(n => n.DeletedAt == null && !n.Archived).ToListAsync().ConfigureAwait(false);

    /// <summary>Open tasks: timed ones first, soonest first, then the rest by their note's last change.</summary>
    public async Task<IReadOnlyList<LibraryItem>> GetTasksAsync()
    {
        await InitAsync().ConfigureAwait(false);
        var items = new List<LibraryItem>();
        foreach (var n in await LiveNotesAsync().ConfigureAwait(false))
            foreach (var t in NoteTasks.Parse(n.Body).Where(t => !t.Done))
                items.Add(new LibraryItem(LibraryItemKind.Task, n.Id, t.Text.Length > 0 ? t.Text : "Untitled task", n.Title, "",
                    n.UpdatedAt, false, false, t.Line, NoteTasks.ReminderAt(t), t.Done));
        return items
            .OrderBy(i => i.Due is null)
            .ThenBy(i => i.Due)
            .ThenByDescending(i => i.UpdatedAt)
            .ThenBy(i => i.TaskLine)
            .ToList();
    }

    public async Task<int> OpenTaskCountAsync() => (await GetTasksAsync().ConfigureAwait(false)).Count;

    public async Task<Note?> ToggleTaskAsync(int noteId, int line)
    {
        await InitAsync().ConfigureAwait(false);
        var note = await _db.FindAsync<Note>(noteId).ConfigureAwait(false);
        if (note is null)
            return null;
        note.Body = NoteTasks.Toggle(note.Body, line);
        return await SaveNoteAsync(note).ConfigureAwait(false);
    }

    public async Task<Note?> SetTaskDueAsync(int noteId, int line, DateTime? due, bool hasTime = true)
    {
        await InitAsync().ConfigureAwait(false);
        var note = await _db.FindAsync<Note>(noteId).ConfigureAwait(false);
        if (note is null)
            return null;
        note.Body = NoteTasks.SetDue(note.Body, line, due, hasTime);
        return await SaveNoteAsync(note).ConfigureAwait(false);
    }

    /// <summary>The note quick tasks are added to.</summary>
    public const string TasksNoteTitle = "Tasks";

    /// <summary>Adds a task to the note called Tasks, which is created on first use.</summary>
    public async Task<Note> AddTaskAsync(string text, DateTime? due)
    {
        await InitAsync().ConfigureAwait(false);
        var note = (await LiveNotesAsync().ConfigureAwait(false))
            .Where(n => n.Title == TasksNoteTitle)
            .OrderByDescending(n => n.UpdatedAt)
            .FirstOrDefault() ?? new Note { Body = $"# {TasksNoteTitle}\n" };
        var line = "- [ ] " + text.ReplaceLineEndings(" ").Trim() + (due is { } d ? " " + NoteTasks.Mark(d) : "");
        var body = note.Body.TrimEnd('\n');
        // Tasks follow straight after a list, or after one blank line below a heading or text.
        var last = body.Split('\n')[^1].TrimStart();
        note.Body = body + (last.StartsWith("- ", StringComparison.Ordinal) ? "\n" : "\n\n") + line + "\n";
        return await SaveNoteAsync(note).ConfigureAwait(false);
    }

    /// <summary>Open tasks whose reminder time falls in (after, until], in local time.</summary>
    public async Task<IReadOnlyList<TaskReminder>> RemindersBetweenAsync(DateTime after, DateTime until)
    {
        await InitAsync().ConfigureAwait(false);
        var list = new List<TaskReminder>();
        foreach (var n in await LiveNotesAsync().ConfigureAwait(false))
            foreach (var t in NoteTasks.Parse(n.Body))
                if (!t.Done && NoteTasks.ReminderAt(t) is { } at && at > after && at <= until)
                    list.Add(new TaskReminder(n.Id, n.Title, t.Line, t.Text, at));
        return list.OrderBy(r => r.At).ToList();
    }

    /// <summary>The soonest reminder after a moment, in local time, or null when none is set.</summary>
    public async Task<DateTime?> NextReminderAsync(DateTime after) =>
        (await RemindersBetweenAsync(after, DateTime.MaxValue).ConfigureAwait(false)).FirstOrDefault()?.At;

    // Renaming (docs/SPEC.md 6.1)

    public async Task<Note?> RenameNoteAsync(int noteId, string title)
    {
        await InitAsync().ConfigureAwait(false);
        var note = await _db.FindAsync<Note>(noteId).ConfigureAwait(false);
        if (note is null || string.IsNullOrWhiteSpace(title))
            return note;
        note.Body = NoteTitles.Rename(note.Body, title);
        return await SaveNoteAsync(note).ConfigureAwait(false);
    }

    /// <summary>Renames the Library entry only; the file itself is never changed. The extension is kept.</summary>
    public async Task RenameDocumentAsync(int documentId, string name)
    {
        await InitAsync().ConfigureAwait(false);
        var doc = await _db.FindAsync<DocumentEntry>(documentId).ConfigureAwait(false);
        name = name.ReplaceLineEndings(" ").Trim();
        if (doc is null || name.Length == 0)
            return;
        var ext = Path.GetExtension(doc.DisplayName);
        if (ext.Length > 0 && !name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            name += ext;
        doc.DisplayName = name;
        await _db.UpdateAsync(doc).ConfigureAwait(false);
    }

    private static string Snippet(string body)
    {
        var lines = body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Skip(1);
        var first = lines.FirstOrDefault() ?? "";
        first = first.TrimStart('#', '>', '-', '*', ' ').Replace("**", "").Replace("[ ]", "").Replace("[x]", "");
        return first.Length > 120 ? first[..120] : first;
    }

    private static string DocSnippet(DocumentEntry d, int highlights, bool linked)
    {
        var parts = new List<string>();
        if (d.Progress > 0.01)
            parts.Add($"{Math.Round(d.Progress * 100)}% read");
        if (highlights > 0)
            parts.Add(highlights == 1 ? "1 highlight" : $"{highlights} highlights");
        if (linked)
            parts.Add("linked note");
        return parts.Count > 0 ? string.Join(", ", parts) : DocumentLoader.Badge(d.Kind) + " document";
    }
}
