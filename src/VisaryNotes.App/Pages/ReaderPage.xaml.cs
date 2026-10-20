using System.Globalization;
using System.Text.Json;
using System.Web;
using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;
using VisaryNotes.Core.Documents;
using VisaryNotes.Core.Rendering;

namespace VisaryNotes.App.Pages;

/// <summary>Reading and Original views with highlights and Quote to note (docs/SPEC.md 7.3 to 7.8).</summary>
public partial class ReaderPage : ContentPage, IQueryAttributable
{
    private readonly LibraryRepository _repo;
    private readonly DocumentService _docs;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private OpenedDocument? _opened;
    private ViewMode _mode = ViewMode.Reading;
    private double _progress;
    private bool _chrome = true;
    private int _highlightCount;
    private readonly WebChrome _chromeSync;
    private CancellationTokenSource? _pdfRender;

    public ReaderPage(LibraryRepository repo, DocumentService docs, SettingsService settings, ThemeService themes)
    {
        _repo = repo;
        _docs = docs;
        _settings = settings;
        _themes = themes;
        InitializeComponent();

        _chromeSync = new WebChrome(Web, () => Top, () => Dock);
        _chromeSync.Track(Top, Dock, Pill, ModeBar);

        Modes.SetItems(["Reading", "Original"], 0);
        Modes.SelectedChanged += async (_, i) =>
        {
            _mode = (ViewMode)i;
            await RenderAsync();
        };
        Web.Navigating += OnWebNavigating;
        Web.Navigated += async (_, _) =>
        {
            await Web.EvaluateJavaScriptAsync($"window.vnScrollTo && window.vnScrollTo({_progress.ToString(CultureInfo.InvariantCulture)})");
            StartPdfPages();
        };
        NoteView.Navigating += (_, e) =>
        {
            if (e.Url.StartsWith("visary://", StringComparison.Ordinal))
                e.Cancel = true;
        };
        CloseSheet.Content = Icons.Make(Icons.Close);
        CloseSheet.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => ShowSheet(false)) });
        OpenNote.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await OpenLinkedNoteAsync()) });
        SaveButton.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await SaveToLibraryAsync()) });
        Thought.Completed += async (_, _) => await AddThoughtAsync();
        Pill.HandSwiped += (_, hand) =>
        {
            _settings.Hand = hand;
            BuildPill();
        };
        SizeChanged += (_, _) => Sheet.HeightRequest = Height * 0.55;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("doc", out var raw) || !int.TryParse(raw?.ToString(), out var id))
            return;
        _opened = await _docs.OpenEntryAsync(id);
        if (_opened is null)
        {
            await DisplayAlertAsync("Document not found", "This document is no longer available.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        var entry = _opened.Entry;
        _progress = entry.Progress;
        _mode = _opened.Document.Kind == DocumentKind.Markdown ? ViewMode.Reading : (ViewMode)entry.ViewMode;
        // A scanned PDF has no text to reflow, so it opens on its pages.
        if (_opened.Document.Kind == DocumentKind.Pdf && _opened.Document.PlainText().Length == 0)
            _mode = ViewMode.Original;
        Modes.Selected = (int)_mode;
        ModeBar.IsVisible = _opened.Document.Kind != DocumentKind.Markdown;
        FileName.Text = entry.DisplayName;
        SaveButton.IsVisible = !entry.InLibrary;
        Warning.Text = _opened.Warning;
        Warning.IsVisible = _opened.Warning is not null;
        _highlightCount = (await _repo.GetHighlightsAsync(entry.Id)).Count;
        PublishToWidget();
        BuildPill();
        await RenderAsync();
    }

    private void BuildPill()
    {
        Pill.SetActions(
        [
            new PillAction(Icons.Back, "Back to Library", () => Shell.Current.GoToAsync("..")),
            new PillAction(Icons.Notes, _highlightCount > 0 ? $"Notes ({_highlightCount})" : "Notes", () => ShowLinkedNoteAsync(), IsPrimary: true),
            new PillAction(Icons.Share, "Share file", ShareAsync)
        ], _settings.Hand);
    }

    private async Task RenderAsync()
    {
        if (_opened is null)
            return;
        var highlights = await _repo.GetHighlightsAsync(_opened.Entry.Id);
        var settings = new ReaderSettings(_themes.Current, _settings.ReadingFontSize, AllowRemoteImages: _settings.AllowRemoteImages, Solid: _themes.Solid);
        var html = await Task.Run(() => _opened.Document.Html(_mode, settings, highlights));
        Web.Source = new HtmlWebViewSource { Html = html, BaseUrl = "file:///android_asset/" };
        Progress.Text = $"{Math.Round(_progress * 100)}%";
    }

    /// <summary>In Original view a PDF's pages are drawn natively and handed to the page as they finish.</summary>
    private void StartPdfPages()
    {
        _pdfRender?.Cancel();
        if (_opened is not { Document.Kind: DocumentKind.Pdf } || _mode != ViewMode.Original)
            return;
#if ANDROID
        var cancel = new CancellationTokenSource();
        _pdfRender = cancel;
        var width = (int)Math.Min(DeviceDisplay.Current.MainDisplayInfo.Width, DeviceDisplay.Current.MainDisplayInfo.Height) * 3 / 2;
        var key = Path.GetFileNameWithoutExtension(_opened.Entry.Uri);
        _ = Platforms.Android.PdfPages.RenderAsync(_opened.Entry.Uri, key, width,
            (n, url) => MainThread.InvokeOnMainThreadAsync(() => Web.EvaluateJavaScriptAsync($"window.vnPdfPage({n},{JsonSerializer.Serialize(url)})")),
            cancel.Token).ContinueWith(t =>
            {
                if (t.Exception is { } ex && !cancel.IsCancellationRequested)
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Warning.Text = "The pages of this PDF could not be drawn. Reading view still shows its text.";
                        Warning.IsVisible = true;
                    });
            }, TaskScheduler.Default);
#endif
    }

    private async void OnWebNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("file:///android_asset/", StringComparison.Ordinal) || e.Url.StartsWith("data:", StringComparison.Ordinal))
            return;
        e.Cancel = true;

        if (!e.Url.StartsWith("visary://", StringComparison.Ordinal))
        {
            // Links in documents open in the browser, never inside the reader.
            if (Uri.TryCreate(e.Url, UriKind.Absolute, out var link) && link.Scheme is "http" or "https" or "mailto")
                await Launcher.Default.OpenAsync(link);
            return;
        }

        var uri = new Uri(e.Url);
        var args = HttpUtility.ParseQueryString(uri.Query);
        var text = args["text"] ?? "";
        if (_opened is null)
            return;
        var id = _opened.Entry.Id;

        switch (uri.Host)
        {
            case "quote":
                await _repo.QuoteToNoteAsync(id, _opened.Entry.DisplayName, text);
                await MarkAsync(text);
                await ShowLinkedNoteAsync();
                break;
            case "highlight":
                await _repo.AddHighlightAsync(id, text);
                await MarkAsync(text);
                break;
            case "copy":
                await Clipboard.Default.SetTextAsync(text);
                break;
            case "tap":
                SetChrome(!_chrome);
                break;
            case "progress":
                if (double.TryParse(args["f"], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    _progress = f;
                    Progress.Text = $"{Math.Round(f * 100)}%";
                    await _repo.SetProgressAsync(id, f, (int)_mode);
                }
                break;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pdfRender?.Cancel();
        PublishToWidget();
    }

    /// <summary>Keeps the Continue Reading widget current (docs/SPEC.md 9.2).</summary>
    private void PublishToWidget()
    {
#if ANDROID
        if (_opened is not null)
            Platforms.Android.Widgets.WidgetActions.PublishReading(_opened.Entry.DisplayName, _progress);
#endif
    }

    private async Task MarkAsync(string text)
    {
        _highlightCount++;
        BuildPill();
        await Web.EvaluateJavaScriptAsync($"window.vnAddHighlight({JsonSerializer.Serialize(text)})");
    }

    /// <summary>A tap on the page hides all chrome for immersive reading, a second tap shows it (docs/SPEC.md 4.5).</summary>
    private void SetChrome(bool on)
    {
        _chrome = on;
        _chromeSync.Immersive = !on;
        Top.IsVisible = on;
        Dock.IsVisible = on;
    }

    private async Task ShowLinkedNoteAsync()
    {
        if (_opened is null)
            return;
        var note = await _repo.GetLinkedNoteAsync(_opened.Entry.Id);
        SheetTitle.Text = note?.Title ?? "No notes yet";
        var body = note?.Body ?? "Select text in the document and choose **Quote to note**. The quote lands here with a link back to this page.";
        var html = HtmlRenderer.Page(MarkdownRenderer.ToHtml(body), ViewMode.Reading, new ReaderSettings(_themes.Current, 16));
        NoteView.Source = new HtmlWebViewSource { Html = html, BaseUrl = "file:///android_asset/" };
        OpenNote.IsVisible = note is not null;
        ShowSheet(true);
    }

    private void ShowSheet(bool on)
    {
        Sheet.IsVisible = on;
        Dock.IsVisible = !on && _chrome;
    }

    private async Task AddThoughtAsync()
    {
        if (_opened is null || string.IsNullOrWhiteSpace(Thought.Text))
            return;
        var note = await _repo.GetOrCreateLinkedNoteAsync(_opened.Entry.Id, _opened.Entry.DisplayName);
        note.Body = note.Body.TrimEnd('\n') + "\n\n" + Thought.Text.Trim() + "\n";
        await _repo.SaveNoteAsync(note);
        Thought.Text = "";
        await ShowLinkedNoteAsync();
    }

    private async Task OpenLinkedNoteAsync()
    {
        if (_opened is null)
            return;
        var note = await _repo.GetLinkedNoteAsync(_opened.Entry.Id);
        if (note is not null)
            await Shell.Current.GoToAsync($"editor?note={note.Id}");
    }

    private async Task SaveToLibraryAsync()
    {
        if (_opened is null)
            return;
        await _repo.SaveToLibraryAsync(_opened.Entry.Id);
        SaveButton.IsVisible = false;
    }

    private async Task ShareAsync()
    {
        if (_opened is null)
            return;
        await Share.Default.RequestAsync(new ShareFileRequest(_opened.Entry.DisplayName, new ShareFile(_opened.Entry.Uri)));
    }
}
