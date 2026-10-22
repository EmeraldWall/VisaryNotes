using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VisaryNotes.Core.Rendering;

/// <summary>
/// Lays out plain text for the reader (docs/SPEC.md R6, R7). Reading view joins hard wrapped lines into
/// paragraphs that reflow to the screen, and keeps short lines (addresses, verse, signatures) and list items on
/// their own lines. Separator lines become a rule in Reading view and never wrap in Original view.
/// </summary>
public static partial class PlainTextLayout
{
    /// <summary>Lines shorter than this share of the paragraph's longest line end where they are.</summary>
    private const double WrapShare = 0.6;

    /// <summary>A paragraph whose longest line is shorter than this was not hard wrapped.</summary>
    private const int MinWrapWidth = 40;

    /// <summary>A row of dashes, equals signs, underscores, stars, tildes or hashes, optionally spaced.</summary>
    [GeneratedRegex(@"^\s*([-=_*~#])(\s*\1){2,}\s*$")]
    private static partial Regex Rule();

    /// <summary>Bullets, numbers and letters that start a list item.</summary>
    [GeneratedRegex(@"^\s*([-*+•◦]\s|\d{1,3}[.)]\s|[a-zA-Z][.)]\s|\(\w{1,3}\)\s)")]
    private static partial Regex ListItem();

    public static bool IsRule(string line) => Rule().IsMatch(line);

    /// <summary>Reading view: paragraphs at blank lines, wrapped lines joined, rules as hr.</summary>
    public static string Reading(string text)
    {
        var sb = new StringBuilder();
        foreach (var para in text.Replace("\r\n", "\n").Replace('\r', '\n').Split("\n\n"))
        {
            var lines = para.Split('\n').Select(l => l.TrimEnd()).ToList();
            while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0)
                continue;

            var width = lines.Where(l => !IsRule(l)).Select(l => l.Trim().Length).DefaultIfEmpty(0).Max();
            var open = false;
            var previousEndsLine = true;
            foreach (var line in lines)
            {
                if (IsRule(line))
                {
                    if (open)
                        sb.Append("</p>");
                    open = false;
                    sb.Append("<hr>");
                    previousEndsLine = true;
                    continue;
                }
                var trimmed = line.Trim();
                if (!open)
                {
                    sb.Append("<p class=\"plain\">");
                    open = true;
                }
                else if (previousEndsLine || ListItem().IsMatch(line) || char.IsWhiteSpace(line[0]))
                {
                    sb.Append("<br>");
                }
                else
                {
                    sb.Append(' ');
                }
                sb.Append(WebUtility.HtmlEncode(trimmed));
                previousEndsLine = width < MinWrapWidth || trimmed.Length < width * WrapShare || trimmed.EndsWith(':');
            }
            if (open)
                sb.Append("</p>");
        }
        return sb.ToString();
    }

    /// <summary>Original view: the text exactly as written; separator lines are clipped instead of wrapping.</summary>
    public static string Original(string text)
    {
        var sb = new StringBuilder("<pre class=\"original-text\">");
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var esc = WebUtility.HtmlEncode(lines[i]);
            if (IsRule(lines[i]))
            {
                // A block element ends the line itself, so the newline after it is dropped.
                sb.Append("<span class=\"rule-line\">").Append(esc).Append("</span>");
                continue;
            }
            sb.Append(esc);
            if (i < lines.Length - 1)
                sb.Append('\n');
        }
        return sb.Append("</pre>").ToString();
    }
}
