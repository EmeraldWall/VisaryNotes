using System.Text.Json;
using Android.Content;
using Rect = Android.Graphics.Rect;
using Android.Views;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using VisaryNotes.App.Controls;
using AView = Android.Views.View;

namespace VisaryNotes.App.Platforms.Android.Handlers;

/// <summary>Adds a <see cref="SelectionWebView"/>'s actions to the system text selection toolbar.</summary>
public sealed class SelectionWebViewHandler : WebViewHandler
{
    protected override global::Android.Webkit.WebView CreatePlatformView() => new SelectionPlatformWebView(this, Context);

    private sealed class SelectionPlatformWebView(WebViewHandler handler, Context context) : MauiWebView(handler, context)
    {
        private readonly WebViewHandler _handler = handler;

        public override ActionMode? StartActionMode(ActionMode.ICallback? callback, ActionModeType type) =>
            base.StartActionMode(Wrap(callback), type);

        public override ActionMode? StartActionMode(ActionMode.ICallback? callback) =>
            base.StartActionMode(Wrap(callback));

        private ActionMode.ICallback? Wrap(ActionMode.ICallback? callback) =>
            callback is null || _handler.VirtualView is not SelectionWebView { SelectionActions.Count: > 0 } view
                ? callback
                : new Callback(callback, view, this);
    }

    /// <summary>The page's own callback, with the app's actions placed first.</summary>
    private sealed class Callback(ActionMode.ICallback inner, SelectionWebView view, global::Android.Webkit.WebView web) : ActionMode.Callback2
    {
        private const int Group = 0x5649; // "VI"
        private const int FirstId = 0x56490000;

        public override bool OnCreateActionMode(ActionMode? mode, IMenu? menu)
        {
            var created = inner.OnCreateActionMode(mode, menu);
            AddActions(menu);
            return created;
        }

        public override bool OnPrepareActionMode(ActionMode? mode, IMenu? menu)
        {
            var prepared = inner.OnPrepareActionMode(mode, menu);
            AddActions(menu);
            return prepared;
        }

        public override bool OnActionItemClicked(ActionMode? mode, IMenuItem? item)
        {
            if (item is null || item.GroupId != Group)
                return inner.OnActionItemClicked(mode, item);
            var action = view.SelectionActions[item.ItemId - FirstId];
            web.EvaluateJavascript("(function(){var t=String(window.getSelection()).trim();window.getSelection().removeAllRanges();return t;})()",
                new Result(json =>
                {
                    var text = string.IsNullOrEmpty(json) || json == "null" ? "" : JsonSerializer.Deserialize<string>(json) ?? "";
                    if (text.Length > 0)
                        MainThread.BeginInvokeOnMainThread(() => view.Raise(action.Id, text));
                }));
            mode?.Finish();
            return true;
        }

        public override void OnDestroyActionMode(ActionMode? mode) => inner.OnDestroyActionMode(mode);

        public override void OnGetContentRect(ActionMode? mode, AView? target, Rect? outRect)
        {
            if (inner is ActionMode.Callback2 floating)
                floating.OnGetContentRect(mode, target, outRect);
            else
                base.OnGetContentRect(mode, target, outRect);
        }

        /// <summary>The app's actions come first, in the order given; the system's follow.</summary>
        private void AddActions(IMenu? menu)
        {
            if (menu is null || menu.FindItem(FirstId) is not null)
                return;
            for (var i = 0; i < view.SelectionActions.Count; i++)
                menu.Add(Group, FirstId + i, i, view.SelectionActions[i].Label)?.SetShowAsAction(ShowAsAction.Always);
        }
    }

    private sealed class Result(Action<string?> done) : Java.Lang.Object, global::Android.Webkit.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => done(value?.ToString());
    }
}
