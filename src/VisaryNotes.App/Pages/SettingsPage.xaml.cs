using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsService _settings;
    private readonly ThemeService _themes;
    private readonly DocumentService _docs;
    private readonly LibraryRepository _repo;
    private bool _ready;

    public SettingsPage(SettingsService settings, ThemeService themes, DocumentService docs, LibraryRepository repo)
    {
        _settings = settings;
        _themes = themes;
        _docs = docs;
        _repo = repo;
        InitializeComponent();

        Hands.SetItems(["Left", "Centre", "Right"], (int)settings.Hand);
        Hands.SelectedChanged += (_, i) =>
        {
            _settings.Hand = (Handedness)i;
            BuildPill();
        };
        Densities.SetItems(["Comfortable", "Compact", "Dense"], (int)settings.Density);
        Densities.SelectedChanged += (_, i) => _settings.Density = (Density)i;

        FontSizeLabel.Text = $"Text size {settings.ReadingFontSize:0}";
        void Step(int by)
        {
            _settings.ReadingFontSize = Math.Clamp(_settings.ReadingFontSize + by, 14, 28);
            FontSizeLabel.Text = $"Text size {_settings.ReadingFontSize:0}";
        }
        Smaller.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Step(-1)) });
        Larger.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Step(1)) });

        Solid.IsToggled = settings.ReduceTransparency;
        Solid.Toggled += (_, e) =>
        {
            if (!_ready)
                return;
            _settings.ReduceTransparency = e.Value;
            _themes.Apply();
        };
        Remote.IsToggled = settings.AllowRemoteImages;
        Remote.Toggled += (_, e) => _settings.AllowRemoteImages = e.Value;

        CleanRow.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await CleanUpAsync()) });
        ReminderRow.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(ReminderService.OpenExactTimesSettings) });
        ImportRow.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await ImportAsync()) });
        Pill.HandSwiped += (_, hand) =>
        {
            _settings.Hand = hand;
            Hands.Selected = (int)hand;
            BuildPill();
        };
        BuildThemes();
        BuildPill();
        _ready = true;
        // The system page for exact alarms is a separate screen, so the status is read again on return.
        Loaded += (_, _) =>
        {
            ShowReminderStatus();
            if (Window is { } window)
                window.Resumed += (_, _) => ShowReminderStatus();
        };
    }

    private void ShowReminderStatus() => ReminderStatus.Text = ReminderService.ExactTimes
        ? "On. Task reminders arrive on the minute."
        : "Off. Android may delay reminders by a few minutes while the phone is idle. Tap to allow exact times.";

    private void BuildPill() =>
        Pill.SetActions([new PillAction(Icons.Back, "Back to Library", () => Shell.Current.GoToAsync(".."), IsPrimary: true)], _settings.Hand);

    private void BuildThemes()
    {
        ThemeList.Clear();
        foreach (var theme in ThemeCatalog.Themes)
        {
            var on = theme.Name == _themes.Current.Name;
            var swatch = new Border
            {
                WidthRequest = ThemeCatalog.Token("size", "badge"),
                HeightRequest = ThemeCatalog.Token("size", "badge"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = ThemeCatalog.Token("radius", "r-1") },
                BackgroundColor = Color.FromArgb(theme.Paper),
                Content = new VerticalStackLayout
                {
                    Spacing = ThemeCatalog.Token("space", "sp-1"),
                    VerticalOptions = LayoutOptions.Center,
                    Padding = new Thickness(ThemeCatalog.Token("space", "sp-1")),
                    Children =
                    {
                        new BoxView { HeightRequest = 4, Color = Color.FromArgb(theme.Ink) },
                        new BoxView { HeightRequest = 4, Color = Color.FromArgb(theme.Ink), WidthRequest = 20, HorizontalOptions = LayoutOptions.Start },
                        new BoxView { HeightRequest = 4, Color = Color.FromArgb(theme.Accent), WidthRequest = 14, HorizontalOptions = LayoutOptions.Start }
                    }
                }
            };
            swatch.SetDynamicResource(Border.StrokeProperty, "LineBrush");

            var note = theme.IsHighContrast ? "Maximum contrast, glass off" : theme.IsDark ? "Dark" : "Light";
            var text = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = theme.Name, Style = (Style)Application.Current!.Resources["T1"] },
                    new Label { Text = theme.Name == ThemeCatalog.DefaultTheme ? "Default. " + note : note, Style = (Style)Application.Current.Resources["T2"] }
                }
            };
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = ThemeCatalog.Token("space", "sp-3"),
                Padding = (Thickness)Application.Current.Resources["InsetRow"],
                MinimumHeightRequest = ThemeCatalog.Token("size", "h-row")
            };
            row.Add(swatch, 0);
            row.Add(text, 1);

            var name = theme.Name;
            var item = new Pressable(() =>
            {
                _themes.Apply(name);
                BuildThemes();
                return Task.CompletedTask;
            })
            { Content = row };
            if (on)
            {
                // Selected state is shown by a bar and a fill, not by colour alone (docs/DESIGN.md 7).
                var bar = new BoxView { WidthRequest = 4, HorizontalOptions = LayoutOptions.Start };
                bar.SetDynamicResource(BoxView.ColorProperty, "Accent");
                row.Add(bar, 0);
                item.SetDynamicResource(BackgroundColorProperty, "Fill");
            }
            SemanticProperties.SetDescription(item, on ? $"{name}, selected" : name);
            ThemeList.Add(item);
            ThemeList.Add(new BoxView { HeightRequest = 1, Color = ThemeService.Res("Line") });
        }
    }

    /// <summary>Clears out documents nobody reads any more, without losing what the user marked in them.</summary>
    private async Task CleanUpAsync()
    {
        var removed = await _repo.CleanUpDocumentsAsync(DateTime.UtcNow.AddDays(-30));
        if (removed.Count == 0)
        {
            _ = Notice.ShowAsync(this, "Nothing to clean up. Every document was opened in the last 30 days.", above: PillRow);
            return;
        }
        _ = Notice.ShowAsync(this, removed.Count == 1 ? "1 document removed" : $"{removed.Count} documents removed", "Undo", async () =>
        {
            foreach (var id in removed)
                await _repo.SaveToLibraryAsync(id);
        }, PillRow);
    }

    private async Task ImportAsync()
    {
        try
        {
            var files = await _docs.PickMarkdownFilesAsync();
            if (files.Count == 0)
                return;
            var count = await _repo.ImportMarkdownAsync(files);
            ImportStatus.Text = count == 1 ? "Imported 1 note" : $"Imported {count} notes";
        }
        catch (Exception ex)
        {
            await BottomSheet.AlertAsync(this, "Import failed", ex.Message);
        }
    }

    /// <summary>Back closes an open sheet before it leaves the page.</summary>
    protected override bool OnBackButtonPressed() => BottomSheet.Dismiss(this) || base.OnBackButtonPressed();
}
