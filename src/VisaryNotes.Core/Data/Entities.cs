using SQLite;
using VisaryNotes.Core.Documents;

namespace VisaryNotes.Core.Data;

[Table("notes")]
public sealed class Note
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Pinned { get; set; }
    public bool Archived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the note is in the recycle bin. Purged after 30 days.</summary>
    [Indexed]
    public DateTime? DeletedAt { get; set; }
}

/// <summary>A document the user has opened, with where they got to.</summary>
[Table("documents")]
public sealed class DocumentEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Uri { get; set; } = "";

    public string DisplayName { get; set; } = "";
    public DocumentKind Kind { get; set; }
    public DateTime LastOpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Reading position from 0 to 1.</summary>
    public double Progress { get; set; }

    public int ViewMode { get; set; }

    /// <summary>False for a file opened from another app and not saved to the Library (docs/SPEC.md 7.7).</summary>
    public bool InLibrary { get; set; }
}

[Table("highlights")]
public sealed class Highlight
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int DocumentId { get; set; }

    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("note_links")]
public sealed class NoteLink
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int NoteId { get; set; }

    [Indexed]
    public int DocumentId { get; set; }

    public bool IsPrimary { get; set; }
}

public enum LibraryFilter
{
    All,
    Notes,
    Documents,
    Linked,
    /// <summary>Notes deleted in the last 30 days, which can be restored or deleted for good.</summary>
    Trash
}

public enum LibraryItemKind
{
    Note,
    Document
}

/// <summary>One row of the unified Library (docs/SPEC.md 6.1).</summary>
public sealed record LibraryItem(
    LibraryItemKind Kind,
    int Id,
    string Title,
    string Snippet,
    string Badge,
    DateTime UpdatedAt,
    bool Linked,
    bool Pinned)
{
    public bool IsDocument => Kind == LibraryItemKind.Document;
}
