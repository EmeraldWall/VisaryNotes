using System.ComponentModel;
using System.Globalization;
using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;

namespace VisaryNotes.App.Pages;

/// <summary>One Library row (docs/SPEC.md 6.1). Selection state changes in place, so it notifies.</summary>
public sealed class LibraryRow : INotifyPropertyChanged
{
    private bool _isSelecting;
    private bool _isSelected;

    public required LibraryItem Item { get; init; }
    public required double RowHeight { get; init; }
    public required bool ShowSnippet { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => Item.Title;
    public string Snippet => Item.Snippet;
    public string Badge => Item.Badge;
    // A task shows when it is due; an overdue one says so in words and in ink, not by colour alone.
    public string DateText => Item.IsTask
        ? Item.Due is { } due ? (due < DateTime.Now ? "Late, " : "") + BottomSheet.When(due) : ""
        : FormatDate(Item.UpdatedAt);
    public Color DateInk => Item.IsTask && Item.Due < DateTime.Now ? ThemeService.Res("Ink") : ThemeService.Res("Soft");
    public string MoreLabel => $"More actions for {Item.Title}";

    // Documents get a filled badge, notes an outlined one, so the two kinds differ without relying on colour.
    public Color BadgeFill => Item.IsDocument ? ThemeService.Res("Ink") : Colors.Transparent;
    public Color BadgeInk => Item.IsDocument ? ThemeService.Res("Paper") : ThemeService.Res("Ink");

    public bool IsSelecting
    {
        get => _isSelecting;
        set => Set(ref _isSelecting, value);
    }

    public bool NotSelecting => !_isSelecting;

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    // Selected rows: a filled check box and a tinted row, so selection never depends on colour alone.
    public Color CheckFill => _isSelected ? ThemeService.Res("Ink") : Colors.Transparent;
    public Color RowFill => _isSelected ? ThemeService.Res("Selection") : ThemeService.Res("Paper");

    /// <summary>Every derived property depends on these two flags, so all bindings refresh together.</summary>
    private void Set(ref bool field, bool value)
    {
        if (field == value)
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

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
    // Tasks took the place of Linked, which is rarely used on its own; linked notes still show under Notes.
    private static readonly (string Label, LibraryFilter Filter)[] Tabs =
        [("All", LibraryFilter.All), ("Notes", LibraryFilter.Notes), ("Docs", LibraryFilter.Documents), ("Tasks", LibraryFilter.Tasks), ("Trash", LibraryFilter.Trash)];

    private readonly LibraryRepository _repo;
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly DocumentService _docs;
    private LibraryFilter _filter = LibraryFilter.All;
    private int? _continueId;
    private List<LibraryRow> _rows = [];
    private bool _selecting;

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

        Filters.SetItems(Tabs.Select(t => t.Label).ToList(), 0);
        Filters.SelectedChanged += async (_, i) =>
        {
            _filter = Tabs[i].Filter;
            BuildPill();
            await LoadAsync();
        };
        SearchEntry.TextChanged += async (_, e) => await SearchAsync(e.NewTextValue);
        _themes.Changed += async (_, _) => await LoadAsync();

        EmptyImport.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await ImportMarkdownAsync()) });
        EmptyTrash.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await EmptyTrashAsync()) });

        SelectClose.Content = Icons.Make(Icons.Close);
        SelectClose.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => SetSelecting(false)) });
        SelectAll.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(ToggleSelectAll) });
        SelectPrimary.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await BulkPrimaryAsync()) });
        SelectSecond.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await BulkSecondAsync()) });
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
            _filter == LibraryFilter.Tasks
                ? new PillAction(Icons.Plus, "New task", NewTaskAsync, IsPrimary: true)
                : new PillAction(Icons.Plus, "New note", () => Shell.Current.GoToAsync("editor"), IsPrimary: true),
            new PillAction(Icons.Search, "Search", () => SetSearching(true))
        ]);
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

        var trash = _filter == LibraryFilter.Trash;
        var tasks = _filter == LibraryFilter.Tasks;
        if (tasks)
            Counts.Text = items.Count == 1 ? "1 open task" : $"{items.Count} open tasks";
        EmptyText.IsVisible = !trash && !tasks && items.Count == 0 && cont is null;
        TrashHeader.IsVisible = trash || tasks && items.Count == 0;
        EmptyTrash.IsVisible = trash && items.Count > 0;
        TrashText.Text = tasks
            ? "No open tasks. Add one with New task, or write \"- [ ]\" in any note. Give it a time and you get a reminder."
            : items.Count == 0
                ? "Trash is empty. Deleted notes wait here for 30 days, so a mistake is never final."
                : "Deleted notes wait here for 30 days, then they are deleted for good. Tap a note to restore it.";
    }

    private void Show(IReadOnlyList<LibraryItem> items)
    {
        var height = _settings.RowHeight;
        var snippet = _settings.Density != Density.Dense;
        _rows = items.Select(i => new LibraryRow { Item = i, RowHeight = height, ShowSnippet = snippet, IsSelecting = _selecting }).ToList();
        List.ItemsSource = _rows;
        UpdateSelectionBar();
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

    private async void OnRowTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LibraryRow row)
            return;
        if (row.Item.IsTask)
        {
            await CompleteTaskAsync(row.Item);
            return;
        }
        if (_selecting)
        {
            ToggleRow(row);
            return;
        }
        if (_filter == LibraryFilter.Trash)
        {
            await TrashActionsAsync(row.Item);
            return;
        }
        await Shell.Current.GoToAsync(Route(row.Item));
    }

    /// <summary>Notes open to read; Edit is one tap away. A new note opens to write.</summary>
    private static string Route(LibraryItem item) => item.IsDocument ? $"reader?doc={item.Id}" : $"editor?note={item.Id}&mode=view";

    private async void OnMoreTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is LibraryRow row)
            await ActionsAsync(row);
    }

    /// <summary>A long press opens the same actions as the more button; in selection mode it selects.</summary>
    private async void OnRowLongPressed(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LibraryRow row)
            return;
        if (_selecting)
        {
            ToggleRow(row);
            return;
        }
        await ActionsAsync(row);
    }

    /// <summary>Row actions in a sheet. Every removal can be undone from the notice that follows.</summary>
    private async Task ActionsAsync(LibraryRow row)
    {
        var item = row.Item;
        if (_filter == LibraryFilter.Trash)
        {
            await TrashActionsAsync(item);
            return;
        }
        if (item.IsTask)
        {
            await TaskActionsAsync(item);
            return;
        }
        string[] options = item.IsDocument
            ? ["Open", "Rename", "Select several", "Remove from Library"]
            : ["Open", "Edit", "Rename", item.Pinned ? "Unpin" : "Pin to top", "Select several", "Delete note"];
        var choice = await BottomSheet.ChooseAsync(this, item.Title, options);
        switch (choice)
        {
            case "Open":
                await Shell.Current.GoToAsync(Route(item));
                break;
            case "Edit":
                await Shell.Current.GoToAsync($"editor?note={item.Id}");
                break;
            case "Rename":
                await RenameAsync(item);
                break;
            case "Pin to top" or "Unpin":
                await _repo.SetPinnedAsync(item.Id, !item.Pinned);
                await LoadAsync();
                break;
            case "Select several":
                StartSelecting(item);
                break;
            case "Delete note" or "Remove from Library":
                await RemoveAsync([item]);
                break;
        }
    }

    /// <summary>
    /// A note's name is its first heading, so renaming changes that heading. A document keeps its file as it is;
    /// only the name shown in the Library changes.
    /// </summary>
    private async Task RenameAsync(LibraryItem item)
    {
        var current = item.IsDocument ? Path.GetFileNameWithoutExtension(item.Title) : item.Title;
        var name = await BottomSheet.PromptAsync(this, item.IsDocument ? "Rename document" : "Rename note", current, "Rename",
            item.IsDocument ? "Name in your Library" : "Note title");
        if (name is null || name == current)
            return;
        if (item.IsDocument)
            await _repo.RenameDocumentAsync(item.Id, name);
        else
            await _repo.RenameNoteAsync(item.Id, name);
        await LoadAsync();
        _ = Notice.ShowAsync(this, "Renamed", above: Dock);
    }

    // Tasks (docs/SPEC.md 6.4)

    private async Task NewTaskAsync()
    {
        var text = await BottomSheet.PromptAsync(this, "New task", "", "Next", "What needs doing");
        if (text is null)
            return;
        var when = await ReminderService.AskAsync(this, text, null, Dock);
        await _repo.AddTaskAsync(text, when?.At);
        await ReminderService.RescheduleAsync();
        await LoadAsync();
        _ = Notice.ShowAsync(this, when?.At is { } at ? $"Task added. Reminder {BottomSheet.When(at)}" : "Task added to your Tasks note", above: Dock);
    }

    /// <summary>Ticks a task off; Undo puts it back.</summary>
    private async Task CompleteTaskAsync(LibraryItem item)
    {
        await _repo.ToggleTaskAsync(item.Id, item.TaskLine);
        await ReminderService.RescheduleAsync();
        await LoadAsync();
        _ = Notice.ShowAsync(this, "Task done", "Undo", async () =>
        {
            await _repo.ToggleTaskAsync(item.Id, item.TaskLine);
            await ReminderService.RescheduleAsync();
            await LoadAsync();
        }, Dock);
    }

    private async Task TaskActionsAsync(LibraryItem item)
    {
        var options = new List<string> { "Mark done", item.Due is null ? "Remind me" : "Change reminder", "Open note" };
        var choice = await BottomSheet.ChooseAsync(this, item.Title, options, $"In {item.Snippet}");
        switch (choice)
        {
            case "Mark done":
                await CompleteTaskAsync(item);
                break;
            case "Open note":
                await Shell.Current.GoToAsync($"editor?note={item.Id}&mode=view");
                break;
            case "Remind me" or "Change reminder":
                if (await ReminderService.AskAsync(this, item.Title, item.Due, Dock) is not { } when)
                    return;
                await _repo.SetTaskDueAsync(item.Id, item.TaskLine, when.At);
                await ReminderService.RescheduleAsync();
                await LoadAsync();
                break;
        }
    }

    private async Task TrashActionsAsync(LibraryItem item)
    {
        var choice = await BottomSheet.ChooseAsync(this, item.Title, ["Restore", "Select several", "Delete forever"], item.Snippet);
        switch (choice)
        {
            case "Restore":
                await _repo.RestoreAsync(item.Id);
                await LoadAsync();
                _ = Notice.ShowAsync(this, "Note restored", above: Dock);
                break;
            case "Select several":
                StartSelecting(item);
                break;
            case "Delete forever":
                await DeleteForeverAsync([item]);
                break;
        }
    }

    // Selection mode (docs/UX-REVIEW.md, round 2): act on many rows at once.

    private void SetSelecting(bool on)
    {
        _selecting = on;
        foreach (var r in _rows)
        {
            r.IsSelecting = on;
            if (!on)
                r.IsSelected = false;
        }
        PillRow.IsVisible = !on;
        FilterBar.IsVisible = !on;
        SelectBar.IsVisible = on;
        UpdateSelectionBar();
    }

    /// <summary>Selection mode, starting with the row the user acted on.</summary>
    private void StartSelecting(LibraryItem item)
    {
        SetSelecting(true);
        if (_rows.FirstOrDefault(r => r.Item == item) is { } row)
            ToggleRow(row);
    }

    private void ToggleRow(LibraryRow row)
    {
        row.IsSelected = !row.IsSelected;
        UpdateSelectionBar();
    }

    private List<LibraryRow> Selected => _rows.Where(r => r.IsSelected).ToList();

    private void UpdateSelectionBar()
    {
        if (!_selecting)
            return;
        var selected = Selected;
        SelectCount.Text = selected.Count == 1 ? "1 selected" : $"{selected.Count} selected";
        if (_filter == LibraryFilter.Trash)
        {
            SelectSecondText.Text = "Restore";
            SelectPrimaryText.Text = "Delete forever";
        }
        else
        {
            var notes = selected.Where(r => !r.Item.IsDocument).ToList();
            SelectSecondText.Text = notes.Count > 0 && notes.All(r => r.Item.Pinned) ? "Unpin" : "Pin";
            SelectSecond.IsVisible = notes.Count > 0;
            SelectPrimaryText.Text = "Delete";
        }
        SelectPrimary.Opacity = selected.Count > 0 ? 1 : 0.4;
        SelectSecond.Opacity = selected.Count > 0 ? 1 : 0.4;
        SemanticProperties.SetDescription(SelectPrimary, SelectPrimaryText.Text);
        SemanticProperties.SetDescription(SelectSecond, SelectSecondText.Text);
    }

    private void ToggleSelectAll()
    {
        var all = _rows.All(r => r.IsSelected);
        foreach (var r in _rows)
            r.IsSelected = !all;
        UpdateSelectionBar();
    }

    private async Task BulkPrimaryAsync()
    {
        var items = Selected.Select(r => r.Item).ToList();
        if (items.Count == 0)
            return;
        if (_filter == LibraryFilter.Trash)
            await DeleteForeverAsync(items);
        else
            await RemoveAsync(items);
    }

    private async Task BulkSecondAsync()
    {
        var items = Selected.Select(r => r.Item).ToList();
        if (items.Count == 0)
            return;
        if (_filter == LibraryFilter.Trash)
        {
            foreach (var item in items)
                await _repo.RestoreAsync(item.Id);
            SetSelecting(false);
            await LoadAsync();
            _ = Notice.ShowAsync(this, items.Count == 1 ? "1 note restored" : $"{items.Count} notes restored", above: Dock);
            return;
        }
        var notes = items.Where(i => !i.IsDocument).ToList();
        var pin = !notes.All(n => n.Pinned);
        foreach (var n in notes)
            await _repo.SetPinnedAsync(n.Id, pin);
        SetSelecting(false);
        await LoadAsync();
    }

    /// <summary>Notes go to the Trash and documents leave the Library; both come back with Undo.</summary>
    private async Task RemoveAsync(IReadOnlyList<LibraryItem> items)
    {
        foreach (var item in items)
        {
            if (item.IsDocument)
                await _repo.RemoveFromLibraryAsync(item.Id);
            else
                await _repo.MoveToTrashAsync(item.Id);
        }
        SetSelecting(false);
        await LoadAsync();
        var notes = items.Count(i => !i.IsDocument);
        var docs = items.Count - notes;
        var message = (notes, docs) switch
        {
            (1, 0) => "Note moved to Trash",
            (_, 0) => $"{notes} notes moved to Trash",
            (0, 1) => "Removed from Library. Highlights are kept.",
            (0, _) => $"{docs} documents removed. Highlights are kept.",
            _ => $"{notes + docs} items removed"
        };
        _ = Notice.ShowAsync(this, message, "Undo", async () =>
        {
            foreach (var item in items)
            {
                if (item.IsDocument)
                    await _repo.SaveToLibraryAsync(item.Id);
                else
                    await _repo.RestoreAsync(item.Id);
            }
            await LoadAsync();
        }, Dock);
    }

    /// <summary>The one action that cannot be undone, so it asks first.</summary>
    private async Task DeleteForeverAsync(IReadOnlyList<LibraryItem> items)
    {
        var title = items.Count == 1 ? $"Delete \"{items[0].Title}\" for good?" : $"Delete {items.Count} notes for good?";
        if (!await BottomSheet.ConfirmAsync(this, title, "This cannot be undone.", "Delete forever"))
            return;
        foreach (var item in items)
            await _repo.DeleteForeverAsync(item.Id);
        SetSelecting(false);
        await LoadAsync();
    }

    private async Task EmptyTrashAsync()
    {
        if (!await BottomSheet.ConfirmAsync(this, "Empty Trash?", "Every note in the Trash is deleted for good. This cannot be undone.", "Empty Trash"))
            return;
        var count = await _repo.EmptyTrashAsync();
        await LoadAsync();
        _ = Notice.ShowAsync(this, count == 1 ? "1 note deleted for good" : $"{count} notes deleted for good", above: Dock);
    }

    private async Task ImportMarkdownAsync()
    {
        try
        {
            if (await _docs.ImportMarkdownAsync() is not (var count and > 0))
                return;
            await LoadAsync();
            _ = Notice.ShowAsync(this, DocumentService.ImportedText(count), above: Dock);
        }
        catch (Exception ex)
        {
            await BottomSheet.AlertAsync(this, "Import failed", ex.Message);
        }
    }

    /// <summary>Back closes an open sheet, then leaves selection mode, then leaves the page.</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BottomSheet.Dismiss(this))
            return true;
        if (_selecting)
        {
            SetSelecting(false);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
