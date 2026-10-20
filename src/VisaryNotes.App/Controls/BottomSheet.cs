using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Controls;

/// <summary>
/// The app's only popup: a bottom sheet in the Act zone instead of a centred system dialog
/// (docs/DESIGN.md E7). Paper surface with a 2dp ink rule on top and 4dp top corners, 56dp rows and buttons,
/// the primary button filled with the accent on the side of the user's hand. A tap on the scrim or the back
/// button cancels. The sheet is laid over the page's root grid, so it needs no extra window or navigation.
/// </summary>
public static class BottomSheet
{
    private static readonly Dictionary<Page, Action> Open = new();

    /// <summary>A list of choices. Returns the chosen label, or null when cancelled.</summary>
    public static Task<string?> ChooseAsync(ContentPage page, string title, IReadOnlyList<string> options, string? message = null) =>
        ShowAsync(page, title, message, options, primary: null, secondary: "Cancel");

    /// <summary>A question with two answers. Returns true for the accept button.</summary>
    public static async Task<bool> ConfirmAsync(ContentPage page, string title, string message, string accept, string cancel = "Cancel") =>
        await ShowAsync(page, title, message, [], primary: accept, secondary: cancel) == accept;

    /// <summary>A message with one button.</summary>
    public static Task AlertAsync(ContentPage page, string title, string message, string ok = "OK") =>
        ShowAsync(page, title, message, [], primary: ok, secondary: null);

    /// <summary>Closes the open sheet on a page, as the back button does. Returns false if none was open.</summary>
    public static bool Dismiss(Page page)
    {
        if (!Open.TryGetValue(page, out var close))
            return false;
        close();
        return true;
    }

    private static async Task<string?> ShowAsync(ContentPage page, string title, string? message, IReadOnlyList<string> options, string? primary, string? secondary)
    {
        if (page.Content is not Grid root)
            return null;
        Dismiss(page);

        var done = new TaskCompletionSource<string?>();
        var services = IPlatformApplication.Current!.Services;
        var theme = services.GetRequiredService<ThemeService>().Current;
        var hand = services.GetRequiredService<SettingsService>().Hand;
        var ink = ThemeService.Res("Ink");
        var paper = ThemeService.Res("Paper");
        var line = ThemeService.Res("Line");
        double T(string group, string key) => ThemeCatalog.Token(group, key);
        Thickness Inset(string key) => (Thickness)Application.Current!.Resources[key];

        // The scrim dims the page so the sheet reads as the only thing to act on; black in every theme.
        var scrim = new BoxView { Color = Colors.Black, Opacity = 0 };
        var targetScrim = theme.IsDark ? 0.6 : 0.32;

        var content = new VerticalStackLayout { Padding = Inset("InsetSheet"), Spacing = T("space", "sp-3") };
        var heading = new Label
        {
            Text = title,
            FontFamily = "FigtreeBold",
            FontSize = (double)Application.Current!.Resources["FontTitle"],
            TextColor = ink,
            LineBreakMode = LineBreakMode.WordWrap
        };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        content.Add(heading);
        if (!string.IsNullOrWhiteSpace(message))
        {
            content.Add(new Label
            {
                Text = message,
                FontFamily = "FigtreeMedium",
                FontSize = (double)Application.Current.Resources["FontUi"],
                LineHeight = 1.4,
                TextColor = ink,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        Grid? overlay = null;
        async Task Close(string? result)
        {
            if (done.Task.IsCompleted || overlay is null)
                return;
            Open.Remove(page);
            done.TrySetResult(result);
            await Task.WhenAll(scrim.FadeToAsync(0, 140), overlay.Children.OfType<Border>().First().TranslateToAsync(0, 400, 160, Easing.CubicIn));
            root.Remove(overlay);
        }

        if (options.Count > 0)
        {
            var list = new VerticalStackLayout();
            list.Add(new BoxView { HeightRequest = 1, Color = line });
            foreach (var option in options)
            {
                var row = new Pressable(() => Close(option))
                {
                    MinimumHeightRequest = T("size", "h-primary"),
                    Padding = Inset("InsetRow"),
                    Content = new Label
                    {
                        Text = option,
                        FontFamily = "FigtreeSemiBold",
                        FontSize = (double)Application.Current.Resources["FontUi"],
                        TextColor = ink,
                        VerticalTextAlignment = TextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                };
                SemanticProperties.SetDescription(row, option);
                list.Add(row);
                list.Add(new BoxView { HeightRequest = 1, Color = line });
            }
            // Long lists scroll inside the sheet, which never covers more than two thirds of the page.
            content.Add(new ScrollView { Content = list, MaximumHeightRequest = Math.Max(240, root.Height * 0.66 - 200) });
        }

        var buttons = new Grid { ColumnSpacing = T("space", "sp-2") };
        var labels = new List<(string Text, bool Primary)>();
        if (secondary is not null)
            labels.Add((secondary, false));
        if (primary is not null)
            labels.Add((primary, true));
        // The primary button sits on the side of the thumb; Centre keeps it on the right, as Android does.
        if (hand == Handedness.Left)
            labels.Reverse();
        for (var i = 0; i < labels.Count; i++)
        {
            var (text, isPrimary) = labels[i];
            buttons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var button = new Pressable(() => Close(isPrimary ? text : null))
            {
                HeightRequest = T("size", "h-primary"),
                StrokeThickness = isPrimary ? 0 : 2,
                Stroke = new SolidColorBrush(ink),
                StrokeShape = new RoundRectangle { CornerRadius = T("radius", "r-1") },
                BackgroundColor = isPrimary ? ThemeService.Res("Accent") : paper,
                Content = new Label
                {
                    Text = text,
                    FontFamily = "FigtreeBold",
                    FontSize = (double)Application.Current.Resources["FontTitle"],
                    TextColor = isPrimary ? ThemeService.Res("OnAccent") : ink,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            SemanticProperties.SetDescription(button, text);
            buttons.Add(button, i, 0);
        }
        content.Add(buttons);

        var panel = new Border
        {
            VerticalOptions = LayoutOptions.End,
            StrokeThickness = 0,
            BackgroundColor = paper,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(T("radius", "r-2"), T("radius", "r-2"), 0, 0) },
            Content = new VerticalStackLayout { Children = { new BoxView { HeightRequest = 2, Color = ink }, content } },
            TranslationY = 400
        };

        overlay = new Grid { ZIndex = 1000 };
        overlay.Add(scrim);
        overlay.Add(panel);
        scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Close(null)) });
        Grid.SetRowSpan(overlay, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(overlay, Math.Max(1, root.ColumnDefinitions.Count));
        root.Add(overlay);
        Open[page] = () => _ = Close(null);

        await Task.WhenAll(scrim.FadeToAsync(targetScrim, 160), panel.TranslateToAsync(0, 0, 200, Easing.CubicOut));
        return await done.Task;
    }
}
