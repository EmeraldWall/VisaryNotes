using System.Globalization;
using System.Net;
using System.Text;
using VisaryNotes.Core.Documents;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.Core.Rendering;

public enum ViewMode
{
    Reading,
    Original
}

public sealed record ReaderSettings(Theme Theme, double FontSizePx = 17, string ReadingFont = "Literata", bool AllowRemoteImages = false);

/// <summary>
/// Turns the document model into a self contained HTML page for the in app WebView (docs/SPEC.md 7.3 and 7.4).
/// The page talks back to the app through visary:// navigations, which the app intercepts and cancels.
/// </summary>
public static class HtmlRenderer
{
    /// <summary>Fonts bundled with the app, and the metric compatible or closest substitute for common document fonts.</summary>
    private static readonly Dictionary<string, (string Css, string? Note)> FontMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Times New Roman"] = ("'Tinos', 'Times New Roman', serif", "Times New Roman shown as Tinos, same metrics"),
        ["Times"] = ("'Tinos', serif", null),
        ["Liberation Serif"] = ("'Tinos', serif", null),
        ["Tinos"] = ("'Tinos', serif", null),
        ["Georgia"] = ("Georgia, 'Literata', serif", null),
        ["Cambria"] = ("'Literata', serif", "Cambria shown as Literata"),
        ["Calibri"] = ("sans-serif", "Calibri shown as the system sans serif"),
        ["Arial"] = ("Arial, sans-serif", null),
        ["Helvetica"] = ("Helvetica, Arial, sans-serif", null),
        ["Liberation Sans"] = ("sans-serif", null),
        ["Courier New"] = ("'Courier New', 'JetBrains Mono', monospace", null),
        ["Consolas"] = ("'JetBrains Mono', monospace", "Consolas shown as JetBrains Mono"),
        ["Liberation Mono"] = ("'JetBrains Mono', monospace", null)
    };

    public static string Render(Document doc, ViewMode mode, ReaderSettings settings, IReadOnlyCollection<string>? highlights = null)
    {
        var body = new StringBuilder();
        var notes = new SortedSet<string>();

        if (mode == ViewMode.Reading)
        {
            body.Append("<main class=\"reading\">");
            if (!string.IsNullOrWhiteSpace(doc.Title) && doc.Format != DocumentFormat.PlainText && doc.Blocks.FirstOrDefault() is not Heading)
                body.Append("<h1 class=\"doc-title\">").Append(Esc(doc.Title)).Append("</h1>");
            foreach (var b in doc.Blocks)
                RenderBlock(b, body, mode, notes, 0);
            body.Append("</main>");
        }
        else
        {
            RenderPages(doc, body, notes);
        }

        return Page(body.ToString(), mode, settings, doc.Page, highlights, notes);
    }

    /// <summary>Wraps already rendered HTML (for example Markdown from Markdig) in the themed reader page.</summary>
    public static string Page(string bodyHtml, ViewMode mode, ReaderSettings settings, PageSetup? page = null,
        IReadOnlyCollection<string>? highlights = null, IEnumerable<string>? notes = null)
    {
        var t = settings.Theme;
        var css = new StringBuilder();
        css.Append(":root{");
        foreach (var (k, v) in t.Colors)
            css.Append("--").Append(k).Append(':').Append(v).Append(';');
        css.Append("--read-size:").Append(Num(settings.FontSizePx)).Append("px;");
        css.Append("--read-font:'").Append(settings.ReadingFont).Append("',Georgia,serif;}");
        css.Append(BaseCss);

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, maximum-scale=5\">");
        sb.Append("<style>").Append(css).Append("</style></head><body class=\"").Append(mode == ViewMode.Reading ? "mode-reading" : "mode-original").Append("\">");
        sb.Append(bodyHtml);
        var noteList = notes?.ToList() ?? [];
        if (noteList.Count > 0)
        {
            sb.Append("<p class=\"notice\">");
            sb.Append(Esc(string.Join(". ", noteList)));
            sb.Append("</p>");
        }
        sb.Append("<div id=\"vn-menu\" role=\"toolbar\" aria-label=\"Selection\" hidden>");
        sb.Append("<button data-act=\"highlight\">Highlight</button>");
        sb.Append("<button data-act=\"quote\" class=\"primary\">Quote to note</button>");
        sb.Append("<button data-act=\"copy\">Copy</button></div>");
        sb.Append("<script>window.vnHighlights=").Append(JsonArray(highlights ?? [])).Append(";").Append(BridgeJs).Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static void RenderPages(Document doc, StringBuilder body, SortedSet<string> notes)
    {
        var p = doc.Page;
        var pageStyle = string.Create(CultureInfo.InvariantCulture,
            $"width:{p.WidthMm:0.##}mm;min-height:{p.HeightMm:0.##}mm;padding:{p.MarginTopMm:0.##}mm {p.MarginRightMm:0.##}mm {p.MarginBottomMm:0.##}mm {p.MarginLeftMm:0.##}mm;");
        var columns = p.Columns > 1 ? $" style=\"column-count:{p.Columns};column-gap:8mm\"" : "";

        body.Append("<div class=\"desk\" data-page-width-mm=\"").Append(Num(p.WidthMm)).Append("\">");
        var pageNumber = 1;
        void Open()
        {
            body.Append("<section class=\"page\" style=\"").Append(pageStyle).Append("\">");
            if (doc.HeaderText is not null)
                body.Append("<header class=\"page-header\">").Append(Esc(doc.HeaderText)).Append("</header>");
            body.Append("<div class=\"page-body\"").Append(columns).Append('>');
        }
        void Close()
        {
            body.Append("</div>");
            body.Append("<footer class=\"page-footer\">").Append(Esc(doc.FooterText ?? $"Page {pageNumber}")).Append("</footer>");
            body.Append("</section>");
            pageNumber++;
        }

        Open();
        foreach (var b in doc.Blocks)
        {
            if (b is PageBreak)
            {
                Close();
                Open();
                continue;
            }
            RenderBlock(b, body, ViewMode.Original, notes, 0);
        }
        Close();
        body.Append("</div>");
    }

    private static void RenderBlock(Block block, StringBuilder sb, ViewMode mode, SortedSet<string> notes, int depth)
    {
        if (depth > ReadLimits.MaxNestingDepth)
            return;
        var original = mode == ViewMode.Original;

        switch (block)
        {
            case Heading h:
                sb.Append("<h").Append(h.Level).Append(original ? ParaAttr(h.Style) : "").Append('>');
                RenderRuns(h.Runs, sb, original, notes);
                sb.Append("</h").Append(h.Level).Append('>');
                break;
            case Paragraph p:
                if (p.IsEmpty)
                {
                    if (original)
                        sb.Append("<p class=\"empty\">&#8203;</p>");
                    break;
                }
                sb.Append("<p").Append(original ? ParaAttr(p.Style) : "").Append('>');
                RenderRuns(p.Runs, sb, original, notes);
                sb.Append("</p>");
                break;
            case ListBlock l:
                sb.Append(l.Ordered ? "<ol>" : "<ul>");
                foreach (var item in l.Items)
                {
                    sb.Append("<li>");
                    foreach (var b in item)
                        RenderBlock(b, sb, mode, notes, depth + 1);
                    sb.Append("</li>");
                }
                sb.Append(l.Ordered ? "</ol>" : "</ul>");
                break;
            case TableBlock t:
                if (!original && t.Rows.Count > 0 && t.Rows.Max(r => r.Count) == 2)
                {
                    // Two column tables read best on a phone as label and value pairs.
                    sb.Append("<dl class=\"pairs\">");
                    foreach (var row in t.Rows)
                    {
                        sb.Append("<div><dt>");
                        foreach (var b in row[0].Blocks) RenderBlock(b, sb, mode, notes, depth + 1);
                        sb.Append("</dt><dd>");
                        if (row.Count > 1)
                            foreach (var b in row[1].Blocks) RenderBlock(b, sb, mode, notes, depth + 1);
                        sb.Append("</dd></div>");
                    }
                    sb.Append("</dl>");
                    break;
                }
                sb.Append("<div class=\"table-wrap\"><table>");
                foreach (var row in t.Rows)
                {
                    sb.Append("<tr>");
                    foreach (var c in row)
                    {
                        var tag = c.IsHeader ? "th" : "td";
                        sb.Append('<').Append(tag);
                        if (c.ColumnSpan > 1) sb.Append(" colspan=\"").Append(c.ColumnSpan).Append('"');
                        if (c.RowSpan > 1) sb.Append(" rowspan=\"").Append(c.RowSpan).Append('"');
                        sb.Append('>');
                        foreach (var b in c.Blocks) RenderBlock(b, sb, mode, notes, depth + 1);
                        sb.Append("</").Append(tag).Append('>');
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table></div>");
                break;
            case ImageBlock img:
                sb.Append("<figure><img alt=\"").Append(Esc(img.Alt ?? "")).Append("\" src=\"data:").Append(img.MimeType)
                  .Append(";base64,").Append(Convert.ToBase64String(img.Data)).Append('"');
                if (original && img.WidthMm is { } w)
                    sb.Append(" style=\"width:").Append(Num(w)).Append("mm\"");
                sb.Append("></figure>");
                break;
            case Preformatted pre when original:
                sb.Append("<pre class=\"original-text\">").Append(Esc(pre.Text)).Append("</pre>");
                break;
            case Preformatted pre:
                foreach (var para in pre.Text.Split("\n\n"))
                {
                    if (string.IsNullOrWhiteSpace(para))
                        continue;
                    sb.Append("<p class=\"plain\">").Append(Esc(para.Trim('\n')).Replace("\n", "<br>")).Append("</p>");
                }
                break;
            case Quote q:
                sb.Append("<blockquote>");
                foreach (var b in q.Blocks) RenderBlock(b, sb, mode, notes, depth + 1);
                sb.Append("</blockquote>");
                break;
            case HorizontalRule:
                sb.Append("<hr>");
                break;
        }
    }

    private static string ParaAttr(ParagraphStyle s)
    {
        if (s == ParagraphStyle.Default)
            return "";
        var css = new StringBuilder();
        if (s.Align != TextAlign.Start)
            css.Append("text-align:").Append(s.Align switch { TextAlign.Center => "center", TextAlign.End => "right", _ => "justify" }).Append(';');
        if (s.IndentLeftMm != 0) css.Append("margin-left:").Append(Num(s.IndentLeftMm)).Append("mm;");
        if (s.IndentFirstLineMm != 0) css.Append("text-indent:").Append(Num(s.IndentFirstLineMm)).Append("mm;");
        if (s.SpaceBeforePt != 0) css.Append("margin-top:").Append(Num(s.SpaceBeforePt)).Append("pt;");
        if (s.SpaceAfterPt != 0) css.Append("margin-bottom:").Append(Num(s.SpaceAfterPt)).Append("pt;");
        return css.Length == 0 ? "" : $" style=\"{css}\"";
    }

    private static void RenderRuns(IEnumerable<Run> runs, StringBuilder sb, bool original, SortedSet<string> notes)
    {
        foreach (var r in runs)
        {
            var s = r.Style;
            var css = new StringBuilder();
            if (original)
            {
                if (s.FontFamily is { } f)
                {
                    if (FontMap.TryGetValue(f, out var mapped))
                    {
                        css.Append("font-family:").Append(mapped.Css).Append(';');
                        if (mapped.Note is not null)
                            notes.Add(mapped.Note);
                    }
                    else
                    {
                        css.Append("font-family:'").Append(f.Replace("'", "")).Append("',serif;");
                    }
                }
                if (s.FontSizePt is { } pt) css.Append("font-size:").Append(Num(pt)).Append("pt;");
                if (s.Color is { } c && IsHex(c)) css.Append("color:").Append(c).Append(';');
                if (s.Highlight is { } bg && IsHex(bg)) css.Append("background:").Append(bg).Append(';');
            }

            var open = new StringBuilder();
            var close = new StringBuilder();
            void Wrap(string tag)
            {
                open.Append('<').Append(tag).Append('>');
                close.Insert(0, $"</{tag}>");
            }
            if (s.Bold) Wrap("strong");
            if (s.Italic) Wrap("em");
            if (s.Underline) Wrap("u");
            if (s.Strike) Wrap("s");
            if (s.Superscript) Wrap("sup");
            if (s.Subscript) Wrap("sub");
            if (s.Monospace) Wrap("code");

            if (r.Link is not null)
                sb.Append("<a href=\"").Append(Esc(r.Link)).Append("\">");
            if (css.Length > 0)
                sb.Append("<span style=\"").Append(css).Append("\">");
            sb.Append(open).Append(Esc(r.Text).Replace("\n", "<br>")).Append(close);
            if (css.Length > 0)
                sb.Append("</span>");
            if (r.Link is not null)
                sb.Append("</a>");
        }
    }

    private static bool IsHex(string s) => s.Length == 7 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);

    public static string Esc(string s) => WebUtility.HtmlEncode(s);

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string JsonArray(IEnumerable<string> items) =>
        System.Text.Json.JsonSerializer.Serialize(items.ToArray());

    private const string BaseCss = """
        @font-face{font-family:'Literata';src:url('Literata-Regular.ttf')}
        @font-face{font-family:'Literata';font-weight:600;src:url('Literata-SemiBold.ttf')}
        @font-face{font-family:'Tinos';src:url('Tinos-Regular.ttf')}
        @font-face{font-family:'Tinos';font-weight:700;src:url('Tinos-Bold.ttf')}
        @font-face{font-family:'JetBrains Mono';src:url('JetBrainsMono-Regular.ttf')}
        @font-face{font-family:'Figtree';font-weight:500;src:url('Figtree-Medium.ttf')}
        @font-face{font-family:'Figtree';font-weight:600;src:url('Figtree-SemiBold.ttf')}
        @font-face{font-family:'Figtree';font-weight:700;src:url('Figtree-Bold.ttf')}
        *{box-sizing:border-box}
        html{-webkit-text-size-adjust:100%}
        body{margin:0;background:var(--paper);color:var(--ink);font-family:'Figtree',sans-serif;-webkit-tap-highlight-color:transparent}
        a{color:var(--accentText)}
        mark.hl{background:var(--hl);color:inherit;box-shadow:inset 0 -2px 0 var(--accent)}
        ::selection{background:var(--sel)}
        .reading{max-width:70ch;margin:0 auto;padding:64px 24px 200px;font-family:var(--read-font);font-size:var(--read-size);line-height:1.65}
        .reading p,.reading li{margin:0 0 16px}
        .reading h1,.reading h2,.reading h3,.reading h4{font-weight:600;line-height:1.25;margin:24px 0 8px}
        .reading h1{font-size:1.55em}.reading h2{font-size:1.3em}.reading h3{font-size:1.15em}
        .reading img{max-width:100%;height:auto}
        .reading figure{margin:0 0 16px}
        .reading blockquote{margin:0 0 16px;padding:0 0 0 12px;border-left:4px solid var(--accent)}
        .reading pre,.reading code{font-family:'JetBrains Mono',monospace;font-size:.88em}
        .reading pre{overflow-x:auto;padding:12px;background:var(--fill, rgba(0,0,0,.05));border-radius:2px}
        .reading .table-wrap{overflow-x:auto;margin:0 0 16px}
        .reading table{border-collapse:collapse;font-family:'Figtree',sans-serif;font-size:.9em}
        .reading td,.reading th{border:1px solid var(--line);padding:8px;text-align:left;vertical-align:top}
        .reading td p,.reading th p,.reading dd p,.reading dt p{margin:0}
        .reading dl.pairs{margin:0 0 16px;font-family:'Figtree',sans-serif;font-size:.9em;border-top:1px solid var(--line)}
        .reading dl.pairs div{display:flex;justify-content:space-between;gap:12px;padding:8px 0;border-bottom:1px solid var(--line)}
        .reading dl.pairs dt{color:var(--soft)}
        .reading dl.pairs dd{margin:0;font-weight:600;text-align:right}
        .reading .plain{white-space:normal}
        .doc-title{margin-top:0}
        .mode-original{background:var(--desk)}
        .desk{padding:56px 8px 200px;display:flex;flex-direction:column;align-items:center;gap:12px;transform-origin:top center}
        .page{background:#fff;color:#1a1a1a;font-family:'Tinos','Times New Roman',serif;font-size:11pt;line-height:1.35;border:1px solid var(--line);position:relative;display:flex;flex-direction:column}
        .page-body{flex:1}
        .page p{margin:0 0 6pt}
        .page p.empty{margin:0}
        .page h1,.page h2,.page h3,.page h4{margin:0 0 6pt;line-height:1.2}
        .page table{border-collapse:collapse;width:100%}
        .page td,.page th{border:.5pt solid #666;padding:2pt 4pt;vertical-align:top}
        .page img{max-width:100%}
        .page a{color:#1a4db3}
        .page-header,.page-footer{font-size:8pt;color:#444}
        .page-header{border-bottom:.5pt solid #888;margin-bottom:8pt;padding-bottom:3pt}
        .page-footer{text-align:center;margin-top:8pt}
        .original-text{margin:0;font-family:'JetBrains Mono',monospace;font-size:13px;line-height:1.5;white-space:pre;overflow-x:auto;padding:64px 16px 200px;color:var(--ink);background:var(--paper)}
        .notice{font-family:'Figtree',sans-serif;font-size:13px;font-weight:500;text-align:center;color:var(--ink);margin:0 16px 200px}
        .mode-original .notice{margin-top:-180px}
        #vn-menu{position:absolute;z-index:10;display:flex;gap:4px;padding:4px;border-radius:999px;background:var(--glass);border:1px solid var(--line);box-shadow:0 6px 18px rgba(0,0,0,.18);font-family:'Figtree',sans-serif}
        #vn-menu[hidden]{display:none}
        #vn-menu button{min-height:48px;padding:0 14px;border:0;border-radius:999px;background:none;color:var(--ink);font:600 14px 'Figtree',sans-serif}
        #vn-menu button.primary{background:var(--accent);color:var(--onAccent);font-weight:700}
        """;

    private const string BridgeJs = """
        (function(){
          function send(kind, params){
            var q = Object.keys(params||{}).map(function(k){return k+'='+encodeURIComponent(params[k]);}).join('&');
            window.location.href = 'visary://' + kind + (q ? '?' + q : '');
          }
          var menu = document.getElementById('vn-menu');
          var lastText = '';
          function placeMenu(){
            var sel = window.getSelection();
            var text = sel ? sel.toString().trim() : '';
            if (!text || sel.rangeCount === 0){ menu.hidden = true; lastText=''; return; }
            lastText = text;
            var r = sel.getRangeAt(0).getBoundingClientRect();
            menu.hidden = false;
            var below = r.top < window.innerHeight * 0.66;
            var top = (below ? r.bottom + 8 : r.top - menu.offsetHeight - 8) + window.scrollY;
            var left = Math.max(8, Math.min(window.innerWidth - menu.offsetWidth - 8, r.left + r.width/2 - menu.offsetWidth/2)) + window.scrollX;
            menu.style.top = top + 'px'; menu.style.left = left + 'px';
          }
          var t; document.addEventListener('selectionchange', function(){ clearTimeout(t); t = setTimeout(placeMenu, 250); });
          menu.addEventListener('click', function(e){
            var b = e.target.closest('button'); if (!b || !lastText) return;
            e.stopPropagation();
            send(b.getAttribute('data-act'), {text: lastText});
            window.getSelection().removeAllRanges(); menu.hidden = true;
          });
          document.addEventListener('click', function(e){
            if (e.target.closest('a,#vn-menu')) return;
            if (window.getSelection().toString()) return;
            send('tap');
          });
          function markOne(h){
            if (!h) return;
            var w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
            var n;
            while ((n = w.nextNode())){
              if (n.parentNode.closest && n.parentNode.closest('script,style,#vn-menu,mark')) continue;
              var i = n.nodeValue.indexOf(h);
              if (i < 0) continue;
              var range = document.createRange(); range.setStart(n, i); range.setEnd(n, i + h.length);
              var m = document.createElement('mark'); m.className = 'hl'; range.surroundContents(m); break;
            }
          }
          function markAll(){ (window.vnHighlights||[]).forEach(markOne); }
          window.vnAddHighlight = function(h){ (window.vnHighlights = window.vnHighlights || []).push(h); markOne(h); };
          function fit(){
            var desk = document.querySelector('.desk'); if (!desk) return;
            var page = desk.querySelector('.page'); if (!page) return;
            var scale = Math.min(1, (window.innerWidth - 16) / page.offsetWidth);
            desk.style.zoom = scale;
          }
          window.vnFit = fit;
          window.vnScrollTo = function(f){ window.scrollTo(0, f * (document.body.scrollHeight - window.innerHeight)); };
          var p; window.addEventListener('scroll', function(){ clearTimeout(p); p = setTimeout(function(){
            var max = document.body.scrollHeight - window.innerHeight;
            send('progress', {f: max > 0 ? (window.scrollY / max).toFixed(3) : '1'});
          }, 600); });
          window.addEventListener('resize', fit);
          markAll(); fit();
        })();
        """;
}
