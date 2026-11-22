namespace ClickForge.Lite;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var window = new LiteWindow();
        Application.Run(window);
    }
}
