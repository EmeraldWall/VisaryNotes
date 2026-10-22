using Microsoft.Extensions.Logging;
using VisaryNotes.App.Pages;
using VisaryNotes.App.Services;
using VisaryNotes.Core.Data;

namespace VisaryNotes.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
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

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "library.db3");
        builder.Services.AddSingleton(_ => new LibraryRepository(dbPath));
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
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
