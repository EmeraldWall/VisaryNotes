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

    /// <summary>One line of text, such as a new name. Returns the trimmed text, or null when cancelled or empty.</summary>
    public static async Task<string?> PromptAsync(ContentPage page, string title, string initial, string accept, string placeholder = "")
    {
        var entry = new Entry { Text = initial, Placeholder = placeholder, ReturnType = ReturnType.Done, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
        SemanticProperties.SetDescription(entry, title);
        var field = new Border { Content = entry };
        if (Application.Current!.Resources.TryGetValue("Field", out var style) && style is Style s)
            field.Style = s;
        entry.Loaded += (_, _) =>
        {
            entry.Focus();
            entry.CursorPosition = 0;
            entry.SelectionLength = entry.Text?.Length ?? 0;
        };
        TaskCompletionSource<string?>? submit = null;
        entry.Completed += (_, _) => submit?.TrySetResult(accept);
        var shown = ShowAsync(page, title, null, [], accept, "Cancel", field, s => submit = s);
        var result = await shown;
        entry.Unfocus();
        var text = entry.Text?.Trim();
        return result == accept && !string.IsNullOrEmpty(text) ? text : null;
    }

    /// <summary>The answer from <see cref="PickReminderAsync"/>: a time, or none to remove the reminder.</summary>
    public sealed record ReminderChoice(DateTime? At);

    /// <summary>
    /// When to be reminded: quick choices first, as most reminders are later today, tomorrow morning or next week,
    /// then any date and time. Returns null when cancelled.
    /// </summary>
    public static async Task<ReminderChoice?> PickReminderAsync(ContentPage page, string task, DateTime? current)
    {
        var now = DateTime.Now;
        var choices = new List<(string Label, DateTime At)>
        {
            ("In 1 hour", new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddHours(1))
        };
        if (now.Hour < 17)
            choices.Add(("This evening", now.Date.AddHours(18)));
        choices.Add(("Tomorrow morning", now.Date.AddDays(1).AddHours(9)));
        var toMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        choices.Add(("Next Monday", now.Date.AddDays(toMonday == 0 ? 7 : toMonday).AddHours(9)));

        var labels = choices.Select(c => $"{c.Label}, {When(c.At)}").ToList();
        labels.Add("Pick a date and time");
        if (current is not null)
            labels.Add("Remove reminder");
        var message = current is { } c0 ? $"Now set for {When(c0)}" : null;
        var picked = await ChooseAsync(page, string.IsNullOrWhiteSpace(task) ? "Remind me" : $"Remind me: {task}", labels, message);
        if (picked is null)
            return null;
        if (picked == "Remove reminder")
            return new ReminderChoice(null);
        var index = labels.IndexOf(picked);
        if (index < choices.Count)
            return new ReminderChoice(choices[index].At);

        var start = current ?? choices[0].At;
        var ink = ThemeService.Res("Ink");
        var date = new DatePicker { Date = start.Date, MinimumDate = now.Date, TextColor = ink, FontFamily = "FigtreeSemiBold" };
        var time = new TimePicker { Time = start.TimeOfDay, TextColor = ink, FontFamily = "FigtreeSemiBold", Format = "HH:mm" };
        SemanticProperties.SetDescription(date, "Date");
        SemanticProperties.SetDescription(time, "Time");
        var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)], ColumnSpacing = ThemeCatalog.Token("space", "sp-3") };
        row.Add(date, 0, 0);
        row.Add(time, 1, 0);
        if (await ShowAsync(page, "Pick a date and time", null, [], "Set", "Cancel", row) != "Set")
            return null;
        return new ReminderChoice((date.Date ?? start.Date).Date + (time.Time ?? start.TimeOfDay));
    }

    /// <summary>A reminder time as people say it: "Today 18:00", "Tomorrow 09:00", "Mon 3 Nov 09:00".</summary>
    public static string When(DateTime at)
    {
        var today = DateTime.Today;
        var day = at.Date == today ? "Today" : at.Date == today.AddDays(1) ? "Tomorrow" : at.ToString("ddd d MMM", System.Globalization.CultureInfo.CurrentCulture);
        return $"{day} {at:HH:mm}";
    }

    /// <summary>Closes the open sheet on a page, as the back button does. Returns false if none was open.</summary>
    public static bool Dismiss(Page page)
    {
        if (!Open.TryGetValue(page, out var close))
            return false;
        close();
        return true;
    }

    /// <param name="body">Extra controls shown under the message, such as a text field or date pickers.</param>
    /// <param name="onOpen">Receives the sheet's result source, so a control in the body can submit the sheet.</param>
    private static async Task<string?> ShowAsync(ContentPage page, string title, string? message, IReadOnlyList<string> options, string? primary, string? secondary,
        View? body = null, Action<TaskCompletionSource<string?>>? onOpen = null)
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

        if (body is not null)
            content.Add(body);

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
        if (onOpen is not null)
        {
            // Submitting from inside the body, such as the keyboard's Done key, closes the sheet as the primary button does.
            var submit = new TaskCompletionSource<string?>();
            onOpen(submit);
            _ = submit.Task.ContinueWith(t => MainThread.BeginInvokeOnMainThread(() => _ = Close(t.Result)), TaskScheduler.Default);
        }

        await Task.WhenAll(scrim.FadeToAsync(targetScrim, 160), panel.TranslateToAsync(0, 0, 200, Easing.CubicOut));
        return await done.Task;
    }
}
