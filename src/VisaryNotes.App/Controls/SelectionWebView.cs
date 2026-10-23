namespace VisaryNotes.App.Controls;

/// <summary>An action the app adds to the text selection toolbar, such as Quote to note.</summary>
public sealed record SelectionAction(string Id, string Label);

/// <summary>
/// A WebView whose own actions join Android's text selection toolbar, next to Copy, Share and Select all
/// (Platforms/Android/Handlers/SelectionWebViewHandler.cs). There is one toolbar for a selection, never a second
/// menu drawn in the page on top of the system's, which is what the reader had before (docs/SPEC.md R15).
/// </summary>
public sealed class SelectionWebView : WebView
{
    public IReadOnlyList<SelectionAction> SelectionActions { get; set; } = [];

    /// <summary>Raised with the action's Id and the selected text.</summary>
    public event EventHandler<(string Id, string Text)>? SelectionActionChosen;

    internal void Raise(string id, string text) => SelectionActionChosen?.Invoke(this, (id, text));
}
