namespace VisaryNotes.App.Controls;

/// <summary>
/// A list row that answers a tap and a long press (docs/SPEC.md N15). On Android both are the platform's own
/// click and long click (Platforms/Android/Handlers/PressRows.cs), so a long press gives the usual haptic tick
/// and works with TalkBack's "double tap and hold". A tap gesture recogniser would swallow the touch and the
/// long press would never fire, which is what happened before.
/// </summary>
public sealed class PressRow : Grid
{
    public event EventHandler? Tapped;
    public event EventHandler? LongPressed;

    internal void RaiseTapped() => Tapped?.Invoke(this, EventArgs.Empty);
    internal void RaiseLongPressed() => LongPressed?.Invoke(this, EventArgs.Empty);
}
