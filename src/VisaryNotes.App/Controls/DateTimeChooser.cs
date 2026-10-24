using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Controls;

/// <summary>
/// A date and time chooser drawn in the app's own style, shown inside a bottom sheet (docs/DESIGN.md E7,
/// docs/SPEC.md T3). It replaces the Android date and time dialogs, which opened as old style system popups
/// on top of the sheet. A month grid with today outlined and the chosen day filled, then the time as hour and
/// minute steppers with common times one tap away. Every target is at least 44dp, and days before today
/// cannot be chosen.
/// </summary>
public sealed class DateTimeChooser : VerticalStackLayout
{
    private static readonly int[] QuickHours = [9, 12, 15, 18, 21];
    private const int MinuteStep = 5;

    private readonly Label _month = new();
    private readonly Grid _days = new() { RowSpacing = 2, ColumnSpacing = 2 };
    private readonly Label _hour = new();
    private readonly Label _minute = new();
    private readonly Label _summary = new();
    private readonly HorizontalStackLayout _quick = new() { Spacing = ThemeCatalog.Token("space", "sp-2") };
    private DateTime _shownMonth;

    public DateTimeChooser(DateTime start)
    {
        Value = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute / MinuteStep * MinuteStep, 0);
        _shownMonth = new DateTime(Value.Year, Value.Month, 1);
        Spacing = ThemeCatalog.Token("space", "sp-3");

        for (var c = 0; c < 7; c++)
            _days.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        Look(_month, "FigtreeBold", "FontTitle");
        _month.HorizontalTextAlignment = TextAlignment.Center;
        var header = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        header.Add(IconButton(Icons.Back, "Previous month", () => ShowMonth(-1)), 0, 0);
        header.Add(_month, 1, 0);
        header.Add(IconButton(Icons.Forward, "Next month", () => ShowMonth(1)), 2, 0);
        _month.VerticalTextAlignment = TextAlignment.Center;

        Look(_hour, "FigtreeBold", "FontTitle");
        Look(_minute, "FigtreeBold", "FontTitle");
        var time = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star)],
            ColumnSpacing = ThemeCatalog.Token("space", "sp-2")
        };
        time.Add(Stepper(_hour, "hour", h => Value = Value.Date + new TimeSpan((Value.Hour + h + 24) % 24, Value.Minute, 0)), 0, 0);
        var colon = new Label { Text = ":", VerticalTextAlignment = TextAlignment.Center };
        Look(colon, "FigtreeBold", "FontTitle");
        time.Add(colon, 1, 0);
        time.Add(Stepper(_minute, "minute", m => Value = Value.Date + new TimeSpan(Value.Hour, (Value.Minute + m * MinuteStep + 60) % 60, 0)), 2, 0);

        foreach (var hour in QuickHours)
        {
            var label = new Label { Text = $"{hour:00}:00", VerticalTextAlignment = TextAlignment.Center, HorizontalTextAlignment = TextAlignment.Center };
            Look(label, "FigtreeSemiBold", "FontUi");
            var chip = new Pressable(() =>
            {
                Value = Value.Date.AddHours(hour);
                return Task.CompletedTask;
            })
            {
                HeightRequest = ThemeCatalog.Token("size", "h-control") - 4,
                Padding = new Thickness(ThemeCatalog.Token("space", "sp-3"), 0),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = ThemeCatalog.Token("radius", "r-2") },
                Content = label
            };
            chip.SetDynamicResource(Border.StrokeProperty, "LineBrush");
            SemanticProperties.SetDescription(chip, $"Set time to {hour:00}:00");
            _quick.Add(chip);
        }

        Look(_summary, "FigtreeSemiBold", "FontUi");
        _summary.SetDynamicResource(Label.TextColorProperty, "AccentText");

        Add(header);
        Add(WeekdayRow());
        Add(_days);
        Add(new BoxView { HeightRequest = 1, Color = ThemeService.Res("Line") });
        Add(time);
        Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = _quick });
        Add(_summary);
        Refresh();
    }

    public DateTime Value
    {
        get;
        private set
        {
            // Never in the past: a time already gone today moves to the same time tomorrow.
            field = value < DateTime.Now.AddMinutes(-1) && value.Date == DateTime.Today ? value.AddDays(1) : value;
            if (_days.Count > 0)
                Refresh();
        }
    }

    private void ShowMonth(int step)
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var target = _shownMonth.AddMonths(step);
        if (target < first)
            return;
        _shownMonth = target;
        Refresh();
    }

    private void Refresh()
    {
        var culture = CultureInfo.CurrentCulture;
        _month.Text = _shownMonth.ToString("MMMM yyyy", culture);
        _hour.Text = Value.Hour.ToString("00", culture);
        _minute.Text = Value.Minute.ToString("00", culture);
        _summary.Text = $"Remind me {BottomSheet.When(Value)}";
        SemanticProperties.SetDescription(_hour, $"Hour {Value.Hour}");
        SemanticProperties.SetDescription(_minute, $"Minute {Value.Minute}");

        _days.Children.Clear();
        _days.RowDefinitions.Clear();
        var offset = ((int)_shownMonth.DayOfWeek - (int)FirstDay + 7) % 7;
        var count = DateTime.DaysInMonth(_shownMonth.Year, _shownMonth.Month);
        var rows = (offset + count + 6) / 7;
        for (var r = 0; r < rows; r++)
            _days.RowDefinitions.Add(new RowDefinition(44));
        var ink = ThemeService.Res("Ink");
        var soft = ThemeService.Res("Soft");
        for (var d = 1; d <= count; d++)
        {
            var day = new DateTime(_shownMonth.Year, _shownMonth.Month, d);
            var past = day < DateTime.Today;
            var chosen = day == Value.Date;
            var label = new Label
            {
                Text = d.ToString(culture),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = chosen ? ThemeService.Res("OnAccent") : past ? soft.WithAlpha(0.5f) : ink
            };
            Look(label, chosen ? "FigtreeBold" : "FigtreeSemiBold", "FontUi", keepColour: true);
            var cell = new Pressable(past ? null : () =>
            {
                Value = day + Value.TimeOfDay;
                return Task.CompletedTask;
            })
            {
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                StrokeThickness = day == DateTime.Today && !chosen ? 1.5 : 0,
                Stroke = new SolidColorBrush(ink),
                BackgroundColor = chosen ? ThemeService.Res("Accent") : Colors.Transparent,
                WidthRequest = 44,
                HeightRequest = 44,
                HorizontalOptions = LayoutOptions.Center,
                Content = label,
                IsEnabled = !past
            };
            SemanticProperties.SetDescription(cell, day.ToString("dddd d MMMM", culture) + (chosen ? ", chosen" : "") + (past ? ", past" : ""));
            var index = offset + d - 1;
            _days.Add(cell, index % 7, index / 7);
        }
    }

    private static DayOfWeek FirstDay => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    private static Grid WeekdayRow()
    {
        var row = new Grid { ColumnSpacing = 2 };
        var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        for (var c = 0; c < 7; c++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var label = new Label { Text = names[((int)FirstDay + c) % 7], HorizontalTextAlignment = TextAlignment.Center };
            Look(label, "FigtreeMedium", "FontLabel");
            label.SetDynamicResource(Label.TextColorProperty, "Soft");
            row.Add(label, c, 0);
        }
        return row;
    }

    /// <summary>A value between a minus and a plus button, each a full size target.</summary>
    private static Grid Stepper(Label value, string name, Action<int> step)
    {
        value.HorizontalTextAlignment = TextAlignment.Center;
        value.VerticalTextAlignment = TextAlignment.Center;
        var grid = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        grid.Add(TextButton("−", $"Earlier {name}", () => step(-1)), 0, 0);
        grid.Add(value, 1, 0);
        grid.Add(TextButton("+", $"Later {name}", () => step(1)), 2, 0);
        return grid;
    }

    private static Pressable TextButton(string text, string description, Action run) =>
        Pressable.Tool(Pressable.Glyph(text, "FontTitle"), description, run);

    private static Pressable IconButton(string icon, string description, Action run) => Pressable.Tool(Icons.Make(icon), description, run);

    private static void Look(Label label, string font, string size, bool keepColour = false)
    {
        label.FontFamily = font;
        label.FontSize = (double)Application.Current!.Resources[size];
        if (!keepColour)
            label.SetDynamicResource(Label.TextColorProperty, "Ink");
    }
}
