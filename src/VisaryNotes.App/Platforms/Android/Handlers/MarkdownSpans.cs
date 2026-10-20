using System.Text.RegularExpressions;
using Android.Graphics;
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
            // Restyle from the start of the first changed paragraph to the end of the last one.
            var from = start == 0 ? 0 : Math.Max(0, text.LastIndexOf("\n\n", Math.Max(0, start - 1), StringComparison.Ordinal));
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
        Java.Lang.Class.FromType(typeof(StrikethroughSpan))
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
        foreach (var type in OwnSpanTypes)
            foreach (var span in e.GetSpans(from, to, type) ?? [])
                e.RemoveSpan(span);

        var soft = ThemeService.Res("Soft").ToPlatform();
        var accent = ThemeService.Res("AccentText").ToPlatform();
        var fill = ThemeService.Res("Fill").ToPlatform();
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
                e.SetSpan(new ForegroundColorSpan(accent), s + 1, s + 1 + m.Groups[1].Length, Mode);
                e.SetSpan(new ForegroundColorSpan(soft), s + 1 + m.Groups[1].Length, s + m.Length, Mode);
            }

            if (nl < 0)
                break;
            lineStart = nl + 1;
        }
    }
}
