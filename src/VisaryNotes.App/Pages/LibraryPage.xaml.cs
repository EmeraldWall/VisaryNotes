using System.Globalization;
using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;

namespace VisaryNotes.App.Pages;

/// <summary>One Library row (docs/SPEC.md 6.1).</summary>
public sealed class LibraryRow
{
    public required LibraryItem Item { get; init; }
    public required double RowHeight { get; init; }
    public required bool ShowSnippet { get; init; }

    public string Title => Item.Title;
    public string Snippet => Item.Snippet;
    public string Badge => Item.Badge;
    public string DateText => FormatDate(Item.UpdatedAt);
    public string PinText => Item.IsDocument ? "Remove" : Item.Pinned ? "Unpin" : "Pin";
    public string DeleteText => Item.IsDocument ? "Remove" : "Delete";

    // Documents get a filled badge, notes an outlined one, so the two kinds differ without relying on colour.
    public Color BadgeFill => Item.IsDocument ? ThemeService.Res("Ink") : Colors.Transparent;
    public Color BadgeInk => Item.IsDocument ? ThemeService.Res("Paper") : ThemeService.Res("Ink");

    public static string FormatDate(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var today = DateTime.Today;
        if (local.Date == today) return local.ToString("HH:mm", CultureInfo.CurrentCulture);
        if (local.Date == today.AddDays(-1)) return "Yesterday";
        if (local.Date > today.AddDays(-7)) return local.ToString("ddd", CultureInfo.CurrentCulture);
        return local.ToString("d MMM", CultureInfo.CurrentCulture);
    }
}

public partial class LibraryPage : ContentPage
{
    private static readonly string[] FilterLabels = ["All", "Notes", "Docs", "Linked"];

    private readonly LibraryRepository _repo;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly DocumentService _docs;
    private LibraryFilter _filter = LibraryFilter.All;
    private int? _continueId;

    public LibraryPage(LibraryRepository repo, SettingsService settings, ThemeService themes, DocumentService docs)
    {
        _repo = repo;
        _settings = settings;
        _themes = themes;
        _docs = docs;
        InitializeComponent();

        SettingsButton.Content = Icons.Make(Icons.Settings);
        SettingsButton.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Shell.Current.GoToAsync("settings")) });
        CancelSearch.Content = Icons.Make(Icons.Close);
        CancelSearch.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await SetSearching(false)) });
        ContinueRow.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () =>
        {
            if (_continueId is { } id)
                await Shell.Current.GoToAsync($"reader?doc={id}");
        }) });

        Filters.SetItems(FilterLabels, 0);
        Filters.SelectedChanged += async (_, i) =>
        {
            _filter = (LibraryFilter)i;
            await LoadAsync();
        };
        SearchEntry.TextChanged += async (_, e) => await SearchAsync(e.NewTextValue);
        Pill.HandSwiped += (_, hand) =>
        {
            _settings.Hand = hand;
            BuildPill();
        };
        _themes.Changed += async (_, _) => await LoadAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        BuildPill();
        await LoadAsync();
    }

    private void BuildPill()
    {
        Pill.SetActions(
        [
            new PillAction(Icons.Folder, "Open file", OpenFileAsync),
            new PillAction(Icons.Plus, "New note", () => Shell.Current.GoToAsync("editor"), IsPrimary: true),
            new PillAction(Icons.Search, "Search", () => SetSearching(true))
        ], _settings.Hand);
    }

    private async Task LoadAsync()
    {
        var items = await _repo.GetLibraryAsync(_filter);
        Show(items);
        var (notes, docs) = await _repo.CountsAsync();
        Counts.Text = $"{notes} {(notes == 1 ? "note" : "notes")}, {docs} {(docs == 1 ? "document" : "documents")}";

        var cont = await _repo.GetContinueReadingAsync();
        _continueId = cont?.Id;
        ContinueRow.IsVisible = cont is not null && _filter is LibraryFilter.All or LibraryFilter.Documents;
        if (cont is not null)
        {
            ContinueBadge.Text = Core.Documents.DocumentLoader.Badge(cont.Kind);
            ContinueTitle.Text = $"Continue: {Path.GetFileNameWithoutExtension(cont.DisplayName)}";
            ContinueProgress.Text = $"{Math.Round(cont.Progress * 100)}%";
            ContinueBar.WidthRequest = Math.Max(4, (Width - 96) * cont.Progress);
            ContinueRow.SetDynamicResource(BackgroundColorProperty, "Fill");
        }
        EmptyText.IsVisible = items.Count == 0 && cont is null;
    }

    private void Show(IReadOnlyList<LibraryItem> items)
    {
        var height = _settings.RowHeight;
        var snippet = _settings.Density != Density.Dense;
        List.ItemsSource = items.Select(i => new LibraryRow { Item = i, RowHeight = height, ShowSnippet = snippet }).ToList();
    }

    private async Task SearchAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await LoadAsync();
            return;
        }
        Show(await _repo.SearchAsync(text));
    }

    private async Task SetSearching(bool on)
    {
        Pill.IsVisible = !on;
        SearchRow.IsVisible = on;
        if (on)
        {
            SearchEntry.Focus();
        }
        else
        {
            SearchEntry.Text = "";
            SearchEntry.Unfocus();
            await LoadAsync();
        }
    }

    private async Task OpenFileAsync()
    {
        try
        {
            var opened = await _docs.PickAndOpenAsync();
            if (opened is not null)
                await Shell.Current.GoToAsync($"reader?doc={opened.Entry.Id}");
        }
        catch (Exception ex)
        {
            await BottomSheet.AlertAsync(this, "Could not open the file", ex.Message);
        }
    }

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LibraryRow row)
            return;
        var route = row.Item.IsDocument ? $"reader?doc={row.Item.Id}" : $"editor?note={row.Item.Id}";
        await Shell.Current.GoToAsync(route);
    }

    private async void OnPinInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LibraryRow row)
            return;
        if (row.Item.IsDocument)
            await _repo.RemoveDocumentAsync(row.Item.Id);
        else
            await _repo.SetPinnedAsync(row.Item.Id, !row.Item.Pinned);
        await LoadAsync();
    }

    private async void OnDeleteInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LibraryRow row)
            return;
        if (row.Item.IsDocument)
            await _repo.RemoveDocumentAsync(row.Item.Id);
        else
            await _repo.MoveToTrashAsync(row.Item.Id);
        await LoadAsync();
    }

    /// <summary>Back closes an open sheet before it leaves the page.</summary>
    protected override bool OnBackButtonPressed() => BottomSheet.Dismiss(this) || base.OnBackButtonPressed();
}
