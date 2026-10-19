using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;
using VisaryNotes.Core.Rendering;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Pages;

/// <summary>Markdown note editor with autosave (docs/SPEC.md 6.2).</summary>
public partial class EditorPage : ContentPage, IQueryAttributable
{
    private readonly LibraryRepository _repo;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private Note _note = new() { Body = "" };
    private CancellationTokenSource? _saveDelay;
    private bool _loading;

    public EditorPage(LibraryRepository repo, SettingsService settings, ThemeService themes)
    {
        _repo = repo;
        _settings = settings;
        _themes = themes;
        InitializeComponent();

        BuildToolbar();
        Body.TextChanged += (_, _) => ScheduleSave();
        Preview.Navigating += async (_, e) =>
        {
            if (e.Url.StartsWith("file:///android_asset/", StringComparison.Ordinal))
                return;
            e.Cancel = true;
            if (e.Url.StartsWith(LibraryRepository.DocumentLinkScheme, StringComparison.Ordinal)
                && int.TryParse(e.Url[LibraryRepository.DocumentLinkScheme.Length..].TrimEnd('/'), out var docId))
                await Shell.Current.GoToAsync($"reader?doc={docId}");
            else if (Uri.TryCreate(e.Url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto")
                await Launcher.Default.OpenAsync(u);
        };
        Pill.HandSwiped += (_, hand) =>
        {
            _settings.Hand = hand;
            BuildPill();
        };
        Dock.SizeChanged += (_, _) =>
        {
            var inset = (Thickness)Application.Current!.Resources["InsetReading"];
            Body.Margin = new Thickness(inset.Left, inset.Top, inset.Right, Dock.Height);
        };
        BuildPill();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _loading = true;
        if (query.TryGetValue("note", out var raw) && int.TryParse(raw?.ToString(), out var id) && await _repo.GetNoteAsync(id) is { } note)
            _note = note;
        else if (query.TryGetValue("text", out var shared) && shared is string sharedText)
            _note = new Note { Body = sharedText };
        else if (query.TryGetValue("template", out var t) && t?.ToString() == "checklist")
            _note = new Note { Body = "# Checklist\n\n- [ ] " };
        else
            _note = new Note { Body = "# " };

        Body.Text = _note.Body;
        NoteTitle.Text = _note.Id == 0 ? "New note" : _note.Title;
        SaveState.Text = _note.Id == 0 ? "" : "Saved";
        _loading = false;
        Body.Focus();
        Body.CursorPosition = Body.Text.Length;
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        _saveDelay?.Cancel();
        await SaveAsync();
    }

    private void BuildPill()
    {
        Pill.SetActions(
        [
            new PillAction(Icons.Back, "Back to Library", () => Shell.Current.GoToAsync("..")),
            new PillAction(Icons.Share, "Share", ShareAsync, IsPrimary: true),
            new PillAction(Icons.Preview, "Preview", TogglePreviewAsync)
        ], _settings.Hand);
    }

    private void BuildToolbar()
    {
        void Add(string label, string glyphOrIcon, Action apply, bool isIcon = false)
        {
            var size = ThemeCatalog.Token("size", "h-control");
            var b = new Pressable(() => { apply(); return Task.CompletedTask; })
            {
                WidthRequest = size,
                HeightRequest = size,
                StrokeShape = new RoundRectangle { CornerRadius = ThemeCatalog.Token("radius", "r-1") }
            };
            b.SetDynamicResource(BackgroundColorProperty, "Fill");
            b.Content = isIcon
                ? Icons.Make(glyphOrIcon)
                : new Label { Text = glyphOrIcon, FontFamily = "FigtreeBold", FontSize = (double)Application.Current!.Resources["FontH3"], HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            SemanticProperties.SetDescription(b, label);
            Toolbar.Add(b);
        }

        Add("Bold", "B", () => Wrap("**"));
        Add("Italic", "I", () => Wrap("*"));
        Add("Heading", "H", () => LinePrefix("## "));
        Add("Bulleted list", "•", () => LinePrefix("- "));
        Add("Checklist", Icons.Check, () => LinePrefix("- [ ] "), isIcon: true);
        Add("Quote", Icons.Quote, () => LinePrefix("> "), isIcon: true);
        Add("Code", "{ }", () => Wrap("`"));
        Add("Link", "↗", () => Wrap("[", "](https://)"));
    }

    private void Wrap(string open, string? close = null)
    {
        close ??= open;
        var text = Body.Text ?? "";
        var start = Math.Clamp(Body.CursorPosition, 0, text.Length);
        var length = Math.Clamp(Body.SelectionLength, 0, text.Length - start);
        var selected = text.Substring(start, length);
        Body.Text = text[..start] + open + selected + close + text[(start + length)..];
        Body.CursorPosition = start + open.Length;
        Body.SelectionLength = selected.Length;
        Body.Focus();
    }

    private void LinePrefix(string prefix)
    {
        var text = Body.Text ?? "";
        var pos = Math.Clamp(Body.CursorPosition, 0, text.Length);
        var lineStart = pos == 0 ? 0 : text.LastIndexOf('\n', pos - 1) + 1;
        Body.Text = text[..lineStart] + prefix + text[lineStart..];
        Body.CursorPosition = pos + prefix.Length;
        Body.Focus();
    }

    private void ScheduleSave()
    {
        if (_loading)
            return;
        SaveState.Text = "Editing";
        _saveDelay?.Cancel();
        _saveDelay = new CancellationTokenSource();
        var token = _saveDelay.Token;
        Task.Delay(800, token).ContinueWith(async t =>
        {
            if (!t.IsCanceled)
                await MainThread.InvokeOnMainThreadAsync(SaveAsync);
        }, TaskScheduler.Default);
    }

    private async Task SaveAsync()
    {
        var body = Body.Text ?? "";
        if (body == _note.Body && _note.Id != 0)
        {
            SaveState.Text = "Saved";
            return;
        }
        if (_note.Id == 0 && string.IsNullOrWhiteSpace(body.Trim('#', ' ', '\n')))
            return;
        _note.Body = body;
        await _repo.SaveNoteAsync(_note);
        NoteTitle.Text = _note.Title;
        SaveState.Text = "Saved";
    }

    private async Task TogglePreviewAsync()
    {
        if (Preview.IsVisible)
        {
            Preview.IsVisible = false;
            Body.IsVisible = true;
            return;
        }
        await SaveAsync();
        var html = HtmlRenderer.Page(MarkdownRenderer.ToHtml(Body.Text ?? ""), ViewMode.Reading,
            new ReaderSettings(_themes.Current, _settings.ReadingFontSize, AllowRemoteImages: _settings.AllowRemoteImages));
        Preview.Source = new HtmlWebViewSource { Html = html, BaseUrl = "file:///android_asset/" };
        Body.IsVisible = false;
        Preview.IsVisible = true;
    }

    /// <summary>Exports in the formats available in v0.1 and hands the file to the Android share sheet (docs/SPEC.md 8.2).</summary>
    private async Task ShareAsync()
    {
        await SaveAsync();
        var choice = await DisplayActionSheetAsync("Share as", "Cancel", null, "Markdown (.md)", "Plain text (.txt)", "Web page (.html)");
        if (choice is null or "Cancel")
            return;
        var name = string.Concat((_note.Title.Length > 0 ? _note.Title : "Note").Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c)));
        var (ext, content) = choice switch
        {
            "Plain text (.txt)" => (".txt", MarkdownRenderer.ToPlainText(_note.Body)),
            "Web page (.html)" => (".html", HtmlRenderer.Page(MarkdownRenderer.ToHtml(_note.Body), ViewMode.Reading, new ReaderSettings(ThemeCatalog.Get("Daylight")))),
            _ => (".md", _note.Body)
        };
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, name + ext);
        await File.WriteAllTextAsync(path, content);
        await Share.Default.RequestAsync(new ShareFileRequest(name, new ShareFile(path)));
    }
}
