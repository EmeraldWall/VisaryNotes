using Microsoft.Extensions.Logging;
using VisaryNotes.App.Pages;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;

namespace VisaryNotes.App;

public static class MauiProgram
{
    /// <summary>The one Library database, also opened by reminders when the app itself is not running.</summary>
    public static string LibraryPath => Path.Combine(FileSystem.AppDataDirectory, "library.db3");

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Controls.SelectionWebView, Platforms.Android.Handlers.SelectionWebViewHandler>();
#endif
            })
            .ConfigureFonts(fonts =>
            {
                // All fonts are bundled under open licences (licenses/ folder at the repository root).
                fonts.AddFont("Figtree-Medium.ttf", "FigtreeMedium");
                fonts.AddFont("Figtree-SemiBold.ttf", "FigtreeSemiBold");
                fonts.AddFont("Figtree-Bold.ttf", "FigtreeBold");
                fonts.AddFont("Literata-Regular.ttf", "Literata");
                fonts.AddFont("Literata-SemiBold.ttf", "LiterataSemiBold");
                fonts.AddFont("JetBrainsMono-Regular.ttf", "Mono");
                fonts.AddFont("Tinos-Regular.ttf", "Tinos");
                fonts.AddFont("Tinos-Bold.ttf", "TinosBold");
            });

        builder.Services.AddSingleton(_ => new LibraryRepository(LibraryPath));
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<DocumentService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LibraryPage>();
        builder.Services.AddTransient<ReaderPage>();
        builder.Services.AddTransient<EditorPage>();
        builder.Services.AddTransient<SettingsPage>();

#if ANDROID
        Platforms.Android.Handlers.MarkdownSpans.Register();
        Platforms.Android.Handlers.PlainFields.Register();
        Platforms.Android.Handlers.PillElevation.Register();
        Platforms.Android.Handlers.WebFiles.Register();
        Platforms.Android.Handlers.SafeInsets.Register();
        Platforms.Android.Handlers.PressRows.Register();
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
