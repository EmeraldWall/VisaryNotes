using VisaryNotes.Core.Data;

namespace VisaryNotes.App.Controls;

/// <summary>
/// The one view for showing a note as it reads, used by the editor's read view and the reader's notes sheet,
/// so both handle links the same way: web and mail links open outside the app, a quote's source link opens the
/// document, and ticked task boxes are reported. Only the app's own page may load inside it.
/// </summary>
public static class NoteWebView
{
    /// <param name="onTask">Called with a ticked box's position among the note's tasks.</param>
    /// <param name="onDocument">Called with a quote's source document; by default the document opens in the reader.</param>
    public static WebView Create(Func<int, Task>? onTask = null, Func<int, Task>? onDocument = null)
    {
        var web = new WebView();
        web.SetDynamicResource(VisualElement.BackgroundColorProperty, "Paper");
        web.Navigating += async (_, e) =>
        {
            if (e.Url.StartsWith("file:///android_asset/", StringComparison.Ordinal) || e.Url.StartsWith("data:", StringComparison.Ordinal))
                return;
            e.Cancel = true;
            const string task = "visary://task?i=";
            if (e.Url.StartsWith(task, StringComparison.Ordinal) && int.TryParse(e.Url[task.Length..], out var index))
            {
                if (onTask is not null)
                    await onTask(index);
            }
            else if (e.Url.StartsWith(LibraryRepository.DocumentLinkScheme, StringComparison.Ordinal)
                     && int.TryParse(e.Url[LibraryRepository.DocumentLinkScheme.Length..].TrimEnd('/'), out var docId))
            {
                await (onDocument ?? (id => Shell.Current.GoToAsync($"reader?doc={id}")))(docId);
            }
            else if (Uri.TryCreate(e.Url, UriKind.Absolute, out var link) && link.Scheme is "http" or "https" or "mailto")
            {
                await Launcher.Default.OpenAsync(link);
            }
        };
        return web;
    }
}
