using VisaryNotes.App.Controls;
using VisaryNotes.App.Services;

namespace VisaryNotes.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services, ThemeService themes)
    {
        _services = services;
        InitializeComponent();
        themes.Apply();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<AppShell>());

    /// <summary>Called by MainActivity when another app sends or opens a file (docs/SPEC.md 7.7).</summary>
    public static async Task OpenIncomingAsync(Stream stream, string fileName)
    {
        await WhenShellReadyAsync();
        var services = IPlatformApplication.Current!.Services;
        var docs = services.GetRequiredService<DocumentService>();
        var opened = await docs.ImportAsync(stream, fileName, addToLibrary: false);
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync($"reader?doc={opened.Entry.Id}"));
    }

    public static async Task ShowErrorAsync(string title, string message)
    {
        await WhenShellReadyAsync();
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.CurrentPage is ContentPage page ? BottomSheet.AlertAsync(page, title, message) : Task.CompletedTask);
    }

    /// <summary>Called for widget buttons, shortcuts and shared text.</summary>
    public static async Task RunActionAsync(string action, string? text = null)
    {
        await WhenShellReadyAsync();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var services = IPlatformApplication.Current!.Services;
            switch (action)
            {
                case "new_note":
                    if (text is null)
                        await Shell.Current.GoToAsync("editor");
                    else
                        await Shell.Current.GoToAsync("editor", new Dictionary<string, object> { ["text"] = text });
                    break;
                case "new_checklist":
                    await Shell.Current.GoToAsync("editor?template=checklist");
                    break;
                case "open_file":
                    var opened = await services.GetRequiredService<DocumentService>().PickAndOpenAsync();
                    if (opened is not null)
                        await Shell.Current.GoToAsync($"reader?doc={opened.Entry.Id}");
                    break;
                case "continue_reading":
                    var doc = await services.GetRequiredService<Core.Data.LibraryRepository>().GetContinueReadingAsync();
                    if (doc is not null)
                        await Shell.Current.GoToAsync($"reader?doc={doc.Id}");
                    break;
            }
        });
    }

    /// <summary>On a cold start from a widget or another app, the intent arrives before the shell exists.</summary>
    private static async Task WhenShellReadyAsync()
    {
        for (var i = 0; i < 100 && (Shell.Current?.CurrentPage is null || !Shell.Current.CurrentPage.IsLoaded); i++)
            await Task.Delay(50);
    }
}
