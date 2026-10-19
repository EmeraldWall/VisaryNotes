using Microsoft.Maui.Controls.Shapes;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Theming;

namespace VisaryNotes.App.Controls;

public sealed record PillAction(string Icon, string Label, Func<Task> Run, bool IsPrimary = false);

/// <summary>
/// The floating action pill (docs/DESIGN.md 4.4 and E3). Holds a screen's two or three main actions,
/// sits left, centre or right for the user's hand, and snaps between those positions when swiped.
/// </summary>
public sealed class ActionPill : GlassView
{
    private readonly HorizontalStackLayout _row = new();
    private IReadOnlyList<PillAction> _actions = [];
    private Handedness _hand = Handedness.Centre;

    public ActionPill()
    {
        IsPill = true;
        _row.Spacing = ThemeCatalog.Token("space", "sp-1");
        _row.Padding = new Thickness(ThemeCatalog.Token("space", "sp-1"));
        Content = _row;
        VerticalOptions = LayoutOptions.End;
        Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.18f, Radius = 18, Offset = new Point(0, 6) };

        var pan = new PanGestureRecognizer();
        double total = 0;
        pan.PanUpdated += (_, e) =>
        {
            if (e.StatusType == GestureStatus.Running)
                total = e.TotalX;
            else if (e.StatusType == GestureStatus.Completed)
            {
                var step = total > 40 ? 1 : total < -40 ? -1 : 0;
                total = 0;
                if (step == 0)
                    return;
                var next = (Handedness)Math.Clamp((int)_hand + step, 0, 2);
                if (next != _hand)
                    HandSwiped?.Invoke(this, next);
            }
        };
        GestureRecognizers.Add(pan);
    }

    /// <summary>Raised when the user swipes the pill to a new side. The page saves it to settings.</summary>
    public event EventHandler<Handedness>? HandSwiped;

    public void SetActions(IReadOnlyList<PillAction> actions, Handedness hand)
    {
        _actions = actions;
        _hand = hand;
        HorizontalOptions = hand switch
        {
            Handedness.Left => LayoutOptions.Start,
            Handedness.Right => LayoutOptions.End,
            _ => LayoutOptions.Center
        };

        // The primary action sits at the end of the pill nearest the thumb, or in the middle for Centre.
        var primary = actions.Where(a => a.IsPrimary).ToList();
        var others = actions.Where(a => !a.IsPrimary).ToList();
        var ordered = hand switch
        {
            Handedness.Left => primary.Concat(others),
            Handedness.Right => others.Concat(primary),
            _ => others.Take(others.Count / 2).Concat(primary).Concat(others.Skip(others.Count / 2))
        };

        _row.Clear();
        foreach (var a in ordered)
            _row.Add(Build(a));
    }

    private static View Build(PillAction a)
    {
        var h = ThemeCatalog.Token("size", "h-control");
        if (!a.IsPrimary)
        {
            var b = new Pressable(a.Run) { WidthRequest = h, HeightRequest = h, StrokeShape = new RoundRectangle { CornerRadius = h / 2 } };
            b.Content = Icons.Make(a.Icon);
            SemanticProperties.SetDescription(b, a.Label);
            return b;
        }

        var label = new Label { Text = a.Label, Style = (Style)Application.Current!.Resources["PillPrimaryText"], VerticalOptions = LayoutOptions.Center };
        var content = new HorizontalStackLayout { Spacing = ThemeCatalog.Token("space", "sp-2"), Children = { Icons.Make(a.Icon, "OnAccentBrush"), label } };
        var primary = new Pressable(a.Run)
        {
            HeightRequest = h,
            Padding = (Thickness)Application.Current.Resources["InsetButton"],
            StrokeShape = new RoundRectangle { CornerRadius = h / 2 },
            Content = content
        };
        primary.SetDynamicResource(BackgroundColorProperty, "Accent");
        SemanticProperties.SetDescription(primary, a.Label);
        return primary;
    }
}

/// <summary>A tappable area with a pressed state, used for pill items, rows and toolbar buttons.</summary>
public class Pressable : Border
{
    private readonly Func<Task>? _run;
    private Color? _rest;

    // XAML needs a parameterless constructor. Pages that declare a Pressable in XAML attach their own tap handler.
    public Pressable() : this(null)
    {
    }

    public Pressable(Func<Task>? run)
    {
        _run = run;
        StrokeThickness = 0;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (_run is not null)
                await _run();
        };
        GestureRecognizers.Add(tap);
        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) =>
        {
            _rest = BackgroundColor;
            if (_rest is null || _rest.Alpha < 0.01)
                BackgroundColor = ThemeService.Res("Fill");
        };
        pointer.PointerReleased += (_, _) => BackgroundColor = _rest;
        pointer.PointerExited += (_, _) => BackgroundColor = _rest;
        GestureRecognizers.Add(pointer);
    }
}
