using System.Text.RegularExpressions;
using Android.Graphics;
using Paint = Android.Graphics.Paint;
using Android.Text;
using Android.Text.Style;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>
/// Live Markdown styling for the note editor (docs/SPEC.md 6.2). Spans are recomputed for the edited
/// paragraphs only, after a short pause in typing, so typing stays smooth on long notes. Syntax marks are
/// dimmed, never hidden.
/// </summary>
public static partial class MarkdownSpans
{
    public static void Register()
    {
        EditorHandler.Mapper.AppendToMapping("VisaryMarkdown", (handler, view) =>
        {
            if (view is not MarkdownEditor || handler.PlatformView.Tag is Java.Lang.String)
                return;
            handler.PlatformView.Tag = new Java.Lang.String("visary-markdown");
            handler.PlatformView.AddTextChangedListener(new Watcher(handler.PlatformView));
            handler.PlatformView.SetLineSpacing(0, 1.35f);
        });
    }

    /// <summary>
    /// Restyles the edited paragraphs once typing pauses for 150ms, not on every keystroke, so fast typing
    /// and the keyboard's own suggestions are never held up by span work.
    /// </summary>
    private sealed class Watcher : Java.Lang.Object, ITextWatcher
    {
        private readonly global::Android.Widget.EditText _view;
        private readonly global::Android.OS.Handler _main = new(global::Android.OS.Looper.MainLooper!);
        private readonly Java.Lang.Runnable _pass;
        private int _start = int.MaxValue;
        private int _end;

        public Watcher(global::Android.Widget.EditText view)
        {
            _view = view;
            _pass = new Java.Lang.Runnable(Restyle);
            // A note's text is set before this watcher is attached, so the first pass styles all of it.
            _start = 0;
            _end = int.MaxValue;
            _main.Post(_pass);
        }

        public void BeforeTextChanged(Java.Lang.ICharSequence? s, int start, int count, int after) { }

        public void OnTextChanged(Java.Lang.ICharSequence? s, int start, int before, int count)
        {
            _start = Math.Min(_start, start);
            _end = Math.Max(_end, start + count);
        }

        public void AfterTextChanged(IEditable? e)
        {
            _main.RemoveCallbacks(_pass);
            _main.PostDelayed(_pass, 150);
        }

        private void Restyle()
        {
            var e = _view.EditableText;
            if (e is null)
                return;
            var text = e.ToString() ?? "";
            var start = Math.Min(_start, text.Length);
            var end = Math.Min(Math.Max(_end, start), text.Length);
            _start = int.MaxValue;
            _end = 0;
            if (text.Length > 200_000)
                return;
            // Restyle from the start of the first changed paragraph to the end of the last one. The range starts
            // after the blank line, so the paragraph before keeps its styles (B3).
            var gap = start == 0 ? -1 : text.LastIndexOf("\n\n", Math.Max(0, start - 1), StringComparison.Ordinal);
            var from = gap < 0 ? 0 : gap + 2;
            var toIdx = text.IndexOf("\n\n", end, StringComparison.Ordinal);
            Apply(e, text, from, toIdx < 0 ? text.Length : toIdx);
        }
    }

    /// <summary>Only the span types this styler adds; the keyboard's own spans are never touched.</summary>
    private static readonly Java.Lang.Class[] OwnSpanTypes =
    [
        Java.Lang.Class.FromType(typeof(StyleSpan)),
        Java.Lang.Class.FromType(typeof(RelativeSizeSpan)),
        Java.Lang.Class.FromType(typeof(ForegroundColorSpan)),
        Java.Lang.Class.FromType(typeof(TypefaceSpan)),
        Java.Lang.Class.FromType(typeof(BackgroundColorSpan)),
        Java.Lang.Class.FromType(typeof(StrikethroughSpan)),
        Java.Lang.Class.FromType(typeof(SourceChip))
    ];

    [GeneratedRegex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"(?<![\*\w])\*(?=\S)([^*\n]+?)(?<=\S)\*(?![\*\w])")]
    private static partial Regex Italic();

    [GeneratedRegex(@"`[^`\n]+`")]
    private static partial Regex Code();

    [GeneratedRegex(@"\[([^\]\n]+)\]\(([^)\n]+)\)")]
    private static partial Regex Link();

    private static void Apply(IEditable e, string text, int from, int to)
    {
        // Only spans inside the range are removed. GetSpans also returns spans that merely touch it, and
        // removing those unstyled the neighbouring heading or quote without restyling it.
        foreach (var type in OwnSpanTypes)
            foreach (var span in e.GetSpans(from, to, type) ?? [])
                if (e.GetSpanStart(span) >= from && e.GetSpanEnd(span) <= to)
                    e.RemoveSpan(span);

        var soft = ThemeService.Res("Soft").ToPlatform();
        var accent = ThemeService.Res("AccentText").ToPlatform();
        var fill = ThemeService.Res("Fill").ToPlatform();
        var chip = ThemeService.Res("Accent").WithAlpha(0.14f).ToPlatform();
        const SpanTypes Mode = SpanTypes.ExclusiveExclusive;

        var lineStart = from;
        while (lineStart <= to && lineStart < text.Length)
        {
            var nl = text.IndexOf('\n', lineStart);
            var lineEnd = nl < 0 || nl > to ? Math.Min(to, text.Length) : nl;
            var line = text[lineStart..lineEnd];

            var hashes = line.TakeWhile(c => c == '#').Count();
            if (hashes is >= 1 and <= 6 && line.Length > hashes && line[hashes] == ' ')
            {
                var scale = hashes switch { 1 => 1.5f, 2 => 1.3f, 3 => 1.15f, _ => 1.05f };
                e.SetSpan(new RelativeSizeSpan(scale), lineStart, lineEnd, Mode);
                e.SetSpan(new StyleSpan(TypefaceStyle.Bold), lineStart + hashes + 1, lineEnd, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), lineStart, lineStart + hashes + 1, Mode);
            }
            else if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                e.SetSpan(new ForegroundColorSpan(soft), lineStart, lineStart + 2, Mode);
                e.SetSpan(new StyleSpan(TypefaceStyle.Italic), lineStart + 2, lineEnd, Mode);
            }
            else if (line.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
            {
                e.SetSpan(new ForegroundColorSpan(soft), lineStart, lineEnd, Mode);
                e.SetSpan(new StrikethroughSpan(), lineStart + 6, lineEnd, Mode);
            }
            else if (line.StartsWith("- [ ] ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                e.SetSpan(new ForegroundColorSpan(soft), lineStart, lineStart + (line.StartsWith("- [ ] ") ? 6 : 2), Mode);
            }

            foreach (Match m in Bold().Matches(line))
            {
                var s = lineStart + m.Index;
                e.SetSpan(new StyleSpan(TypefaceStyle.Bold), s + 2, s + m.Length - 2, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), s, s + 2, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), s + m.Length - 2, s + m.Length, Mode);
            }
            foreach (Match m in Italic().Matches(line))
            {
                var s = lineStart + m.Index;
                e.SetSpan(new StyleSpan(TypefaceStyle.Italic), s + 1, s + m.Length - 1, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), s, s + 1, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), s + m.Length - 1, s + m.Length, Mode);
            }
            foreach (Match m in Code().Matches(line))
            {
                var s = lineStart + m.Index;
                e.SetSpan(new TypefaceSpan("monospace"), s, s + m.Length, Mode);
                e.SetSpan(new BackgroundColorSpan(fill), s, s + m.Length, Mode);
            }
            foreach (Match m in Link().Matches(line))
            {
                var s = lineStart + m.Index;
                if (m.Groups[2].Value.StartsWith(Core.Data.LibraryRepository.DocumentLinkScheme, StringComparison.Ordinal))
                {
                    // A quote's source shows as a chip with the document name, not as link code (B8).
                    e.SetSpan(new SourceChip(m.Groups[1].Value, accent, chip), s, s + m.Length, Mode);
                    continue;
                }
                e.SetSpan(new ForegroundColorSpan(accent), s + 1, s + 1 + m.Groups[1].Length, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), s + 1 + m.Groups[1].Length, s + m.Length, Mode);
            }

            if (nl < 0)
                break;
            lineStart = nl + 1;
        }
    }

    /// <summary>
    /// Draws a quote's source link, "[report.odt](visary-doc://12)", as a small chip that shows only the
    /// document name (docs/SPEC.md E12). The underlying Markdown stays in the note and opens the document in
    /// Preview and in the reader's note sheet.
    /// </summary>
    private sealed class SourceChip : ReplacementSpan
    {
        private const int MaxChars = 40;
        private readonly string _label;
        private readonly global::Android.Graphics.Color _ink;
        private readonly global::Android.Graphics.Color _fill;

        public SourceChip(string name, global::Android.Graphics.Color ink, global::Android.Graphics.Color fill)
        {
            _label = name.Length > MaxChars ? name[..(MaxChars - 1)] + "\u2026" : name;
            _ink = ink;
            _fill = fill;
        }

        private static Paint Small(Paint paint) => new(paint) { TextSize = paint.TextSize * 0.85f };

        private static float Pad(Paint paint) => paint.TextSize * 0.5f;

        public override int GetSize(Paint paint, Java.Lang.ICharSequence? text, int start, int end, Paint.FontMetricsInt? fm)
        {
            if (fm is not null && paint.GetFontMetricsInt() is { } own)
            {
                fm.Ascent = own.Ascent;
                fm.Descent = own.Descent;
                fm.Top = own.Top;
                fm.Bottom = own.Bottom;
            }
            using var small = Small(paint);
            return (int)Math.Ceiling(small.MeasureText(_label) + 2 * Pad(paint));
        }

        public override void Draw(Canvas canvas, Java.Lang.ICharSequence? text, int start, int end, float x, int top, int y, int bottom, Paint paint)
        {
            using var small = Small(paint);
            var width = small.MeasureText(_label) + 2 * Pad(paint);
            var metrics = small.GetFontMetrics()!;
            var chipTop = y + metrics.Ascent - paint.TextSize * 0.2f;
            var chipBottom = y + metrics.Descent + paint.TextSize * 0.2f;
            var radius = (chipBottom - chipTop) / 2;
            using (var back = new Paint(PaintFlags.AntiAlias) { Color = _fill })
            {
                back.SetStyle(Paint.Style.Fill);
                canvas.DrawRoundRect(x, chipTop, x + width, chipBottom, radius, radius, back);
            }
            small.Color = _ink;
            canvas.DrawText(_label, x + Pad(paint), y, small);
        }
    }
}
