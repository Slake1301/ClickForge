using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClickForge.Storage;

namespace ClickForge.UI;

public partial class MainWindow
{
    private bool _syncingAppearance;
    private static readonly Dictionary<string, string> Accents = new()
    {
        ["Violet"] = "Фиолетовый", ["Blue"] = "Синий", ["Teal"] = "Бирюзовый", ["Rose"] = "Розовый"
    };

    private void InitializeAppearance()
    {
        ThemePicker.ItemsSource = new Dictionary<string, string> { ["Dark"] = "Тёмная тема", ["Light"] = "Светлая тема" };
        AccentPicker.ItemsSource = Accents;
        foreach (var picker in new[] { ThemePicker, AccentPicker })
        {
            picker.DisplayMemberPath = "Value";
            picker.SelectedValuePath = "Key";
        }
        LoadAppearance();
    }

    private void LoadAppearance()
    {
        bool light = _viewModel.Settings.Theme == "Light";
        var accent = _viewModel.Settings.AccentColor;
        _syncingAppearance = true;
        try
        {
            ThemePicker.SelectedValue = light ? "Light" : "Dark";
            AccentPicker.SelectedValue = accent != null && Accents.ContainsKey(accent) ? accent : "Violet";
        }
        finally { _syncingAppearance = false; }
        Brush("Panel", light ? "#FFFFFF" : "#121927");
        Brush("ButtonSurface", light ? "#455672" : "#263148");
        Brush("Canvas", light ? "#F1F4FA" : "#0B0F17");
        Brush("Ink", light ? "#192336" : "#E8ECF4");
        Brush("Inset", light ? "#E5EBF5" : "#0E1420");
        Brush("Outline", light ? "#8898B4" : "#2A3650");
        Brush("Muted", light ? "#43536E" : "#A9B3C7");
        Brush("Hero", light ? "#E1E8F7" : "#18193B");
        Brush("Tint", light ? "#49368F" : "#A9A1FF");
        Brush("DangerInk", light ? "#9B2441" : "#FF7D87");
        Brush("Accent", accent switch { "Blue" => "#1659BD", "Teal" => "#08736D", "Rose" => "#A62167", _ => "#624FE0" });
    }

    private void Brush(string name, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        Resources[name] = brush;
    }

    private async void AppearanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingAppearance || !IsLoaded || ThemePicker.SelectedValue is not string theme
            || AccentPicker.SelectedValue is not string accent) return;
        var settings = _viewModel.Settings;
        var oldTheme = settings.Theme;
        var oldAccent = settings.AccentColor;
        if (oldTheme == theme && oldAccent == accent) return;
        settings.Theme = theme;
        settings.AccentColor = accent;
        ThemePicker.IsEnabled = AccentPicker.IsEnabled = false;
        LoadAppearance();
        try
        {
            await _viewModel.SaveSettingsAsync(settings);
            _viewModel.Message = "Оформление сохранено.";
        }
        catch (Exception ex)
        {
            settings.Theme = oldTheme;
            settings.AccentColor = oldAccent;
            _viewModel.Message = ex.Message;
        }
        finally
        {
            LoadAppearance();
            ThemePicker.IsEnabled = AccentPicker.IsEnabled = true;
        }
    }

    private async void ResetSettings(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Вернуть заводские настройки и точки мультикликера? Статистика сохранится.",
            "Сброс настроек", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            != MessageBoxResult.Yes) return;

        var oldKey = Enum.TryParse<Key>(_viewModel.StartHotkey, out var previous) ? previous : Key.F6;
        try
        {
            await _viewModel.EmergencyStopAsync();
            _hotkeys.ChangeStartKey(Key.F6, 1);
            try { await _viewModel.SaveSettingsAsync(JsonSettingsStore.CreateDefaults()); }
            catch
            {
                _hotkeys.ChangeStartKey(oldKey, _viewModel.StartPressCount);
                throw;
            }
            StartKeyPicker.SelectedValue = Key.F6;
            StartPressCountPicker.SelectedValue = 1;
            LoadAppearance();
            _viewModel.Message = "Заводские настройки восстановлены.";
        }
        catch (Exception ex) { _viewModel.Message = ex.Message; }
    }
}
