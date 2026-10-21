namespace VisaryNotes.App.Platforms.Android;

/// <summary>
/// Find in document with the WebView's own search, which highlights every match and scrolls to the
/// current one. Works the same in Reading and Original views.
/// </summary>
public static class WebFind
{
    private static global::Android.Webkit.WebView? Native(WebView web) => web.Handler?.PlatformView as global::Android.Webkit.WebView;

    /// <param name="onResult">Current match (1 based, 0 when none) and total, once counting is done.</param>
    public static void Find(WebView web, string text, Action<int, int> onResult)
    {
        if (Native(web) is not { } view)
            return;
        view.SetFindListener(new Listener(onResult));
        view.FindAllAsync(text);
    }

    public static void Next(WebView web, bool forward) => Native(web)?.FindNext(forward);

    public static void Clear(WebView web) => Native(web)?.ClearMatches();

    private sealed class Listener(Action<int, int> onResult) : Java.Lang.Object, global::Android.Webkit.WebView.IFindListener
    {
        public void OnFindResultReceived(int activeMatchOrdinal, int numberOfMatches, bool isDoneCounting)
        {
            if (isDoneCounting)
                onResult(numberOfMatches == 0 ? 0 : activeMatchOrdinal + 1, numberOfMatches);
        }
    }
}
