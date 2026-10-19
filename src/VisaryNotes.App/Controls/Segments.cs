using Microsoft.Maui.Controls.Shapes;
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
            box.SetDynamicResource(Border.StrokeProperty, "InkBrush");
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
            SemanticProperties.SetDescription(box, labels[i]);
            this.Add(box, i);
            _items.Add((box, text));
        }
        _selected = selected;
        Paint();
    }

    private void Paint()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var on = i == _selected;
            if (on)
            {
                _items[i].Box.SetDynamicResource(BackgroundColorProperty, "Ink");
                _items[i].Text.SetDynamicResource(Label.TextColorProperty, "Paper");
            }
            else
            {
                _items[i].Box.BackgroundColor = Colors.Transparent;
                _items[i].Text.SetDynamicResource(Label.TextColorProperty, "Ink");
            }
        }
    }
}
