using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace VisaryNotes.Core.Rendering;

/// <summary>Markdown to HTML with Markdig (docs/SPEC.md 6.3).</summary>
public static class MarkdownRenderer
{
    // Raw HTML is disabled: Markdown files are untrusted and the viewer must never run their scripts.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .DisableHtml()
        .Build();

    /// <param name="loadImage">Returns bytes and a MIME type for a relative image path, or null when it cannot be found.</param>
    public static string ToHtml(string markdown, Func<string, (byte[] Data, string Mime)?>? loadImage = null, bool allowRemoteImages = false)
    {
        var doc = Markdown.Parse(markdown, Pipeline);

        foreach (var link in doc.Descendants<LinkInline>())
        {
            if (!link.IsImage || string.IsNullOrEmpty(link.Url))
                continue;
            var url = link.Url;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!allowRemoteImages)
                    link.Url = "data:image/gif;base64,R0lGODlhAQABAAAAACw=";
                continue;
            }
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;
            var image = loadImage?.Invoke(Uri.UnescapeDataString(url));
            link.Url = image is { } i ? $"data:{i.Mime};base64,{Convert.ToBase64String(i.Data)}" : "";
        }

        var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        var front = doc.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (front is not null)
        {
            writer.Write("<aside class=\"front-matter\"><pre>");
            writer.Write(System.Net.WebUtility.HtmlEncode(front.Lines.ToString()));
            writer.Write("</pre></aside>");
            doc.Remove(front);
        }
        renderer.Render(doc);
        writer.Flush();
        return "<main class=\"reading\">" + writer + "</main>";
    }

    /// <summary>The first heading, or the first line, used as a note title.</summary>
    public static string TitleOf(string markdown)
    {
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line == "---")
                continue;
            return line.TrimStart('#', ' ').Trim();
        }
        return "Untitled";
    }

    /// <summary>Markdown stripped to plain text for .txt export and search snippets.</summary>
    public static string ToPlainText(string markdown) => Markdown.ToPlainText(markdown, Pipeline).Trim();
}
