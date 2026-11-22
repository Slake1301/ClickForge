using ClickForge.Core;
using ClickForge.Storage;

namespace ClickForge.Lite;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge Lite");
        var store = new JsonSettingsStore(directory);
        AppSettings? settings = null;
        string? loadError = null;
        try
        {
            if (File.Exists(Path.Combine(directory, "settings.json")))
                settings = store.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) { loadError = ex.Message; }

        UiText.Language = settings?.Language ?? "ru";
        using var window = new LiteWindow(directory, store, settings, loadError);
        Application.Run(window);
    }
}
