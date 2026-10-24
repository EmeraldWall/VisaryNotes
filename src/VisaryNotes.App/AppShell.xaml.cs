using VisaryNotes.App.Pages;

namespace VisaryNotes.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("reader", typeof(ReaderPage));
        Routing.RegisterRoute("editor", typeof(EditorPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }
}
