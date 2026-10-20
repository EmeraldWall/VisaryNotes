using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Controls;

/// <summary>
/// A short confirmation with an optional action, such as "Note deleted, Undo". It never blocks the page:
/// no scrim, it sits just above the bottom bar in the Act zone and leaves after five seconds. Inverse
/// colours (ink surface, paper text) set it apart from content. Undo makes every removal safe to try,
/// which is what lets people act without a confirmation step (docs/UX-REVIEW.md).
/// </summary>
public static class Notice
{
    private static readonly Dictionary<Page, View> Shown = new();

    public static async Task ShowAsync(ContentPage page, string message, string? action = null, Func<Task>? run = null, View? above = null)
    {
        if (page.Content is not Grid root)
            return;
        if (Shown.Remove(page, out var old))
            root.Remove(old);

        var ink = ThemeService.Res("Ink");
        var paper = ThemeService.Res("Paper");
        var space = ThemeCatalog.Token("space", "sp-4");
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = ThemeCatalog.Token("space", "sp-2"),
            Padding = new Thickness(space, 0, action is null ? space : 0, 0),
            MinimumHeightRequest = ThemeCatalog.Token("size", "h-control")
        };
        row.Add(new Label
        {
            Text = message,
            TextColor = paper,
            FontFamily = "FigtreeSemiBold",
            FontSize = (double)Application.Current!.Resources["FontUi"],
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 2
        });

        var bar = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = ink,
            StrokeShape = new RoundRectangle { CornerRadius = ThemeCatalog.Token("radius", "r-2") },
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(space, 0, space, (above?.Height ?? 0) + ThemeCatalog.Token("space", "sp-2")),
            Content = row,
            ZIndex = 900,
            Opacity = 0
        };
        SemanticProperties.SetDescription(bar, action is null ? message : $"{message}. {action} button");

        async Task Hide()
        {
            if (!Shown.TryGetValue(page, out var current) || current != bar)
                return;
            Shown.Remove(page);
            await bar.FadeToAsync(0, 140);
            root.Remove(bar);
        }

        if (action is not null && run is not null)
        {
            var button = new Pressable(async () =>
            {
                await Hide();
                await run();
            })
            {
                MinimumWidthRequest = ThemeCatalog.Token("size", "h-control"),
                HeightRequest = ThemeCatalog.Token("size", "h-control"),
                Padding = new Thickness(space, 0),
                Content = new Label
                {
                    Text = action,
                    TextColor = paper,
                    FontFamily = "FigtreeBold",
                    FontSize = (double)Application.Current.Resources["FontUi"],
                    TextDecorations = TextDecorations.Underline,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            SemanticProperties.SetDescription(button, action);
            row.Add(button, 1, 0);
        }

        Grid.SetRowSpan(bar, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(bar, Math.Max(1, root.ColumnDefinitions.Count));
        root.Add(bar);
        Shown[page] = bar;
        await bar.FadeToAsync(1, 140);
        await Task.Delay(5000);
        await Hide();
    }
}
