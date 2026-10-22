using System.Globalization;
using System.Text.RegularExpressions;

namespace VisaryNotes.Core.Data;

/// <summary>A Markdown task in a note. Line is the zero based line in the note body.</summary>
public sealed record NoteTask(int Line, string Text, bool Done, DateTime? Due, bool HasTime);

/// <summary>
/// Tasks and their reminder times, kept as plain Markdown so the note stays portable (docs/SPEC.md 6.4).
/// A task is a list item with a box, "- [ ] Call Sam". A time is written after it in the reminder format
/// "(@2026-10-23 14:00)" or, for a whole day, "(@2026-10-23)". Times are the phone's local time.
/// </summary>
public static partial class NoteTasks
{
    /// <summary>Whole day tasks remind at this hour.</summary>
    public const int DayReminderHour = 9;

    // Matches what the Markdown renderer treats as a task, so the boxes in a note's view map to these lines:
    // a bullet or number, then a box. Markdig takes "- [ ]c" as a task too.
    [GeneratedRegex(@"^(\s*(?:[-*+]|\d{1,9}[.)])\s+\[)( |x|X)(\]\s?)(.*)$")]
    private static partial Regex TaskLine();

    [GeneratedRegex(@"\s*\(@(\d{4}-\d{2}-\d{2})(?:[ T](\d{1,2}:\d{2}))?\)")]
    private static partial Regex DueMark();

    public static IReadOnlyList<NoteTask> Parse(string body)
    {
        var tasks = new List<NoteTask>();
        var lines = Lines(body);
        var fenced = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }
            if (fenced || TaskLine().Match(lines[i]) is not { Success: true } m)
                continue;
            var rest = m.Groups[4].Value;
            var (due, hasTime) = ReadDue(rest);
            var text = DueMark().Replace(rest, "").Trim();
            tasks.Add(new NoteTask(i, text, m.Groups[2].Value != " ", due, hasTime));
        }
        return tasks;
    }

    /// <summary>When a task reminds: its time, or the morning of its day.</summary>
    public static DateTime? ReminderAt(NoteTask task) =>
        task.Due is not { } due ? null : task.HasTime ? due : due.Date.AddHours(DayReminderHour);

    public static string Toggle(string body, int line) => EditLine(body, line, l =>
        TaskLine().Match(l) is { Success: true } m
            ? m.Groups[1].Value + (m.Groups[2].Value == " " ? "x" : " ") + m.Groups[3].Value + m.Groups[4].Value
            : l);

    /// <summary>Sets or clears a line's reminder. A line that is not a task becomes one.</summary>
    public static string SetDue(string body, int line, DateTime? due, bool hasTime = true) => EditLine(body, line, l =>
    {
        string head, rest;
        if (TaskLine().Match(l) is { Success: true } m)
        {
            head = m.Groups[1].Value + m.Groups[2].Value + "] ";
            rest = m.Groups[4].Value;
        }
        else
        {
            var indent = l[..(l.Length - l.TrimStart().Length)];
            var content = l.TrimStart();
            // A plain list item keeps its bullet; anything else gets one.
            if (content.Length > 1 && content[0] is '-' or '*' or '+' && content[1] == ' ')
                content = content[2..];
            head = indent + "- [ ] ";
            rest = content;
        }
        rest = DueMark().Replace(rest, "").TrimEnd();
        return due is { } d ? $"{head}{rest} {Mark(d, hasTime)}" : head + rest;
    });

    public static string Mark(DateTime due, bool hasTime = true) =>
        "(@" + due.ToString(hasTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd", CultureInfo.InvariantCulture) + ")";

    private static (DateTime? Due, bool HasTime) ReadDue(string text)
    {
        var m = DueMark().Match(text);
        if (!m.Success || !DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return (null, false);
        if (m.Groups[2].Success && TimeSpan.TryParseExact(m.Groups[2].Value, ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out var time) && time < TimeSpan.FromDays(1))
            return (day + time, true);
        return (day, false);
    }

    private static string[] Lines(string body) => body.Replace("\r\n", "\n").Split('\n');

    private static string EditLine(string body, int line, Func<string, string> edit)
    {
        var lines = Lines(body);
        if (line < 0 || line >= lines.Length)
            return body;
        lines[line] = edit(lines[line]);
        return string.Join('\n', lines);
    }
}

/// <summary>Note titles come from the first line, so renaming a note edits that line (docs/SPEC.md 6.1).</summary>
public static class NoteTitles
{
    public static string Rename(string body, string title)
    {
        title = title.ReplaceLineEndings(" ").Trim();
        if (title.Length == 0)
            return body;
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line == "---")
                continue;
            if (line.StartsWith('#'))
            {
                var hashes = line.TakeWhile(c => c == '#').Count();
                lines[i] = new string('#', hashes) + " " + title;
                return string.Join('\n', lines);
            }
            break;
        }
        // The first line is body text, so the new title goes above it as a heading.
        return $"# {title}\n\n{body.TrimStart('\n')}";
    }
}
