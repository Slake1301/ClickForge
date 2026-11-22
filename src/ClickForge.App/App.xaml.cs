using System.Windows;
using System.IO;
using ClickForge.Automation;
using ClickForge.Diagnostics;
using ClickForge.Hotkeys;
using ClickForge.Input;
using ClickForge.Storage;
using ClickForge.UI;

namespace ClickForge.App;

public partial class App : Application
{
    private AutomationEngine? _engine;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrash(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) => { WriteCrash(args.Exception); MessageBox.Show(args.Exception.Message, "ClickForge", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge");
        Directory.CreateDirectory(data);
        var store = new JsonSettingsStore(data);
        var stats = new StatisticsService(data);
        await stats.LoadAsync();
        var input = new WindowsInputService();
        _engine = new AutomationEngine(input, stats);
        var settings = await store.LoadAsync();
        var vm = new MainViewModel(_engine, input, store, stats, settings);
        MainWindow = new MainWindow(vm, new GlobalHotkeyService());
        MainWindow.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/ClickForge;component/ClickForge.ico"));
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { _engine?.Dispose(); base.OnExit(e); }
    private static void WriteCrash(Exception? ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge", "logs");
            Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "errors.log"), $"{DateTimeOffset.Now:O} {ex}\n");
        }
        catch { }
    }
}
