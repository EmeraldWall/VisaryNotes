using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Controls;

/// <summary>Equal width segmented tabs: filters, view mode, hand and density (2dp control shape).</summary>
public sealed class Segments : Grid
{
    private readonly List<(Border Box, Label Text)> _items = new();
    private int _selected;

    public event EventHandler<int>? SelectedChanged;

    public Segments()
    {
        ColumnSpacing = ThemeCatalog.Token("space", "sp-1");
        Loaded += (_, _) =>
        {
            if (Handler?.MauiContext?.Services.GetService<ThemeService>() is { } themes)
                themes.Changed += (_, _) => Paint();
            Paint();
        };
    }

    public int Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Paint();
        }
    }

    public void SetItems(IReadOnlyList<string> labels, int selected)
    {
        Clear();
        ColumnDefinitions.Clear();
        _items.Clear();
        for (var i = 0; i < labels.Count; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var text = new Label
            {
                Text = labels[i],
                Style = (Style)Application.Current!.Resources["SegmentText"],
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            var box = new Border
            {
                HeightRequest = ThemeCatalog.Token("size", "h-control"),
                StrokeThickness = 1.5,
                StrokeShape = new RoundRectangle { CornerRadius = ThemeCatalog.Token("radius", "r-1") },
                Content = text
            };
            var index = i;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (_selected == index)
                    return;
                Selected = index;
                SelectedChanged?.Invoke(this, index);
            };
            box.GestureRecognizers.Add(tap);
            this.Add(box, i);
            _items.Add((box, text));
        }
        _selected = selected;
        Paint();
    }

    /// <summary>
    /// Selected is solid ink with paper text, the rest are paper with ink text and an ink outline. Colours are
    /// set directly, not as dynamic resources, because a local value would outrank a dynamic resource and freeze
    /// the old state. Paper rather than clear keeps the labels legible over any content.
    /// </summary>
    private void Paint()
    {
        var ink = ThemeService.Res("Ink");
        var paper = ThemeService.Res("Paper");
        for (var i = 0; i < _items.Count; i++)
        {
            var on = i == _selected;
            _items[i].Box.Stroke = new SolidColorBrush(ink);
            _items[i].Box.Background = new SolidColorBrush(on ? ink : paper);
            _items[i].Text.TextColor = on ? paper : ink;
            SemanticProperties.SetDescription(_items[i].Box, on ? $"{_items[i].Text.Text}, selected" : _items[i].Text.Text);
        }
    }
}
