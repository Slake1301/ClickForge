using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace ClickForge.UI;

public partial class FeedbackPanel : UserControl
{
    private static readonly (string Title, string[] Labels)[] Forms =
    [
        ("Сообщить об ошибке", ["Что произошло", "Что ожидалось", "Как повторить"]),
        ("Предложить улучшение", ["Что хочется добавить или изменить", "В какой ситуации это пригодится"]),
        ("Задать вопрос", ["Вопрос"])
    ];

    private readonly string?[][] _drafts = [new string?[3], new string?[3], new string?[3]];
    private int _category = -1;
    private bool _switching;
    private static string VersionText => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "неизвестна";

    public FeedbackPanel()
    {
        InitializeComponent();
        ProductVersion.Text = $"ClickForge {VersionText}";
        Category.SelectedIndex = 0;
    }

    private void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        int next = Category.SelectedIndex;
        if (next < 0 || next == _category) return;
        if (_category >= 0) _drafts[_category] = [FirstInput.Text, SecondInput.Text, ThirdInput.Text];
        _category = next;
        var labels = Forms[next].Labels;
        _switching = true;
        FirstLabel.Text = labels[0];
        SecondLabel.Text = labels.ElementAtOrDefault(1) ?? "";
        ThirdLabel.Text = labels.ElementAtOrDefault(2) ?? "";
        SecondField.Visibility = labels.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        ThirdField.Visibility = labels.Length > 2 ? Visibility.Visible : Visibility.Collapsed;
        FirstInput.Text = _drafts[next][0] ?? "";
        SecondInput.Text = _drafts[next][1] ?? "";
        ThirdInput.Text = _drafts[next][2] ?? "";
        _switching = false;
        RefreshPreview();
    }

    private void InputChanged(object sender, TextChangedEventArgs e)
    {
        if (_category >= 0 && !_switching) RefreshPreview();
    }

    private void RefreshPreview()
    {
        var (title, labels) = Forms[_category];
        string[] fields = [FirstInput.Text.Trim(), SecondInput.Text.Trim(), ThirdInput.Text.Trim()];
        var sections = labels.Select((label, index) => $"{label}:\n{fields[index]}");
        string text = title + "\n\n" + string.Join("\n\n", sections);
        if (_category == 0)
        {
            text += $"\n\nClickForge: {VersionText}\nWindows: {Environment.OSVersion.Version}";
        }
        Preview.Text = text;
        CopyButton.IsEnabled = fields.Take(labels.Length).Any(field => field.Length > 0);
        CopyStatus.Text = "";
    }

    private void CopyFeedback(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Preview.Text);
            CopyStatus.Text = "Обращение скопировано";
        }
        catch (ExternalException)
        {
            CopyStatus.Text = "Буфер обмена недоступен. Попробуйте ещё раз.";
        }
    }

    private void OpenSupportBot(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://t.me/ClickForge_Helpbot") { UseShellExecute = true });
            TelegramStatus.Text = "";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            TelegramStatus.Text = "Не удалось открыть ссылку. Найдите @ClickForge_Helpbot в Telegram.";
        }
    }

    private void OpenAuthorProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/Slake1301") { UseShellExecute = true });
            ProfileStatus.Text = "";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            ProfileStatus.Text = "Не удалось открыть браузер. Профиль на GitHub: Slake1301.";
        }
    }

}
