using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using ClickForge.Automation;
using ClickForge.Core;
using ClickForge.Hotkeys;
using ClickForge.Storage;
using ClickForge.UI;

internal static class FeedbackTests
{
    public static Task RunAsync()
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var language in new[] { "ru", "en" })
                {
                    UiText.Language = language;
                    foreach (var theme in new[] { "Dark", "Light" }) CheckTheme(theme, language);
                }
                result.SetResult();
            }
            catch (Exception ex) { result.SetException(ex); }
            finally { UiText.Language = "ru"; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }

    private static void CheckTheme(string theme, string language)
    {
        var stats = new FakeStats();
        var input = new FakeInput();
        using var engine = new AutomationEngine(input, stats);
        var settings = new AppSettings { Theme = theme, Language = language, CheckUpdatesOnStartup = false };
        var viewModel = new MainViewModel(engine, input, new JsonSettingsStore(System.IO.Path.GetTempPath()), stats, settings);
        var window = new MainWindow(viewModel, new GlobalHotkeyService());
        var picker = (ComboBox)window.FindName("LanguagePicker");
        Require(picker.SelectedIndex == (language == "en" ? 1 : 0), "The language picker must reflect the saved language");
        viewModel.SelectedSection = 5;
        var panel = (FeedbackPanel)window.FindName("Feedback");
        var guide = (HelpGuide)window.FindName("Guide");
        var expanders = Descendants(guide).OfType<Expander>().ToArray();
        Require(expanders.Length == 7 && expanders.All(item => !item.IsExpanded), "All seven guide topics must start collapsed");
        Require(Equals(expanders[0].Header, language == "en" ? "Home" : "Главная"), "Help must use the selected language");
        foreach (var expander in expanders)
        {
            var provider = (IExpandCollapseProvider)new ExpanderAutomationPeer(expander).GetPattern(PatternInterface.ExpandCollapse);
            provider.Expand();
            Require(expander.IsExpanded, "Guide topic did not expand");
            provider.Collapse();
            Require(!expander.IsExpanded, "Guide topic did not collapse");
        }
        var root = (FrameworkElement)window.Content;
        var size = new Size(window.MinWidth, window.MinHeight);
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
        var scroll = (ScrollViewer)window.FindName("PageScroll");
        scroll.ScrollToEnd();
        root.UpdateLayout();
        Require(scroll.ScrollableHeight > 0 && Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1,
            "Help must scroll to the end at minimum size");
        Require(((TextBlock)panel.FindName("ProductVersion")).Text.StartsWith("ClickForge "),
            "About must show the application version");
        var category = (ComboBox)panel.FindName("Category");
        var description = (TextBox)panel.FindName("FirstInput");
        var preview = (TextBox)panel.FindName("Preview");
        var copy = (Button)panel.FindName("CopyButton");
        Require(!copy.IsEnabled, "Empty report must not be copyable");
        description.Text = "Example reproduction";
        Require(copy.IsEnabled && preview.Text.Contains("Example reproduction")
            && preview.Text.Contains("ClickForge:") && preview.Text.Contains("Windows:"), "Bug report preview is incomplete");
        category.SelectedIndex = 1;
        Require(preview.Text.Contains(language == "en" ? "When it would help" : "В какой ситуации")
            && !preview.Text.Contains("Windows:"), "Wrong suggestion fields");
        category.SelectedIndex = 2;
        Require(((FrameworkElement)panel.FindName("SecondField")).Visibility == Visibility.Collapsed
            && !preview.Text.Contains("ClickForge:"), "Question must have one field and no technical data");
        category.SelectedIndex = 0;
        Require(description.Text == "Example reproduction", "Draft was lost after switching category");

        CheckContrast("Ink", "Inset");
        CheckContrast("Ink", "Panel");
        CheckContrast("Muted", "Panel");
        Require(Ratio(Colors.White, GetColor("Accent")) >= 4.5, $"{theme}: insufficient copy button contrast");

        Color GetColor(string name) => ((SolidColorBrush)panel.FindResource(name)).Color;
        void CheckContrast(string foreground, string background) =>
            Require(Ratio(GetColor(foreground), GetColor(background)) >= 4.5, $"{theme}: insufficient {foreground}/{background} contrast");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static double Ratio(Color first, Color second)
    {
        static double Linear(byte value)
        {
            double channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) =>
            0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        double a = Luminance(first);
        double b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
