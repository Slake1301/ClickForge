using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Reflection;
using ClickForge.Hotkeys;

namespace ClickForge.UI;

public partial class MainWindow : Window
{
    private readonly GlobalHotkeyService _hotkeys;
    private readonly MainViewModel _viewModel;
    public MainWindow(MainViewModel viewModel, GlobalHotkeyService hotkeys)
    {
        InitializeComponent();
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is not null) Title = $"ClickForge {version.ToString(3)}";
        DataContext = _viewModel = viewModel;
        _hotkeys = hotkeys;
        InitializeAppearance();
        ContentRendered += CheckUpdatesOnStartup;
        SourceInitialized += (_, _) =>
        {
            _hotkeys.Pressed += OnHotkey;
            StartKeyPicker.DisplayMemberPath = "Value";
            StartKeyPicker.SelectedValuePath = "Key";
            PopulateStartKeys(Enum.TryParse<Key>(_viewModel.StartHotkey, out var key) ? key : Key.F6);
            StartPressCountPicker.ItemsSource = new Dictionary<int, string> { [1] = "1 — одиночное", [2] = "2 — двойное" };
            StartPressCountPicker.DisplayMemberPath = "Value";
            StartPressCountPicker.SelectedValuePath = "Key";
            StartPressCountPicker.SelectedValue = _viewModel.StartPressCount;
            try { _hotkeys.Attach(this, StartKeyPicker.SelectedValue is Key selected ? selected : Key.F6, _viewModel.StartPressCount); }
            catch (Exception ex) { _viewModel.Message = ex.Message; }
        };
    }
    private const Key CustomKey = (Key)1100;
    private bool _capturingStartKey;
    private Key _capturePrevious;
    private void PopulateStartKeys(Key selected)
    {
        var choices = Enumerable.Range((int)Key.F1, 11).Select(value => (Key)value)
            .ToDictionary(key => key, key => key switch
            {
                Key.F7 => "F7 (пауза)", Key.F10 => "F10 (аварийная остановка)", _ => key.ToString()
            });
        if (!choices.ContainsKey(selected)) choices[selected] = GlobalHotkeyService.DisplayName(selected);
        choices[CustomKey] = "Ввести кнопку самому…";
        _capturingStartKey = false;
        StartKeyPicker.ItemsSource = choices;
        StartKeyPicker.SelectedValue = selected;
        _capturePrevious = selected;
    }

    private void StartKeySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StartKeyPicker.SelectedValue is not Key selected) return;
        if (selected == CustomKey)
        {
            _capturingStartKey = true;
            _viewModel.Message = "Нажмите клавишу или кнопку мыши в этом окне. Esc — отмена.";
        }
        else { _capturingStartKey = false; _capturePrevious = selected; }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_capturingStartKey) { base.OnPreviewKeyDown(e); return; }
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { PopulateStartKeys(_capturePrevious); _viewModel.Message = "Ввод отменён."; }
        else if (!e.IsRepeat && KeyInterop.VirtualKeyFromKey(key) != 0) FinishStartKeyCapture(key);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if (!_capturingStartKey) { base.OnPreviewMouseDown(e); return; }
        e.Handled = true;
        var key = e.ChangedButton switch
        {
            MouseButton.Left => GlobalHotkeyService.MouseLeft,
            MouseButton.Right => GlobalHotkeyService.MouseRight,
            MouseButton.Middle => GlobalHotkeyService.MouseMiddle,
            MouseButton.XButton1 => GlobalHotkeyService.MouseBack,
            MouseButton.XButton2 => GlobalHotkeyService.MouseForward,
            _ => Key.None
        };
        if (key != Key.None) FinishStartKeyCapture(key);
    }

    private void FinishStartKeyCapture(Key key)
    {
        if (key is Key.F7 or Key.F10 or Key.F12)
        {
            _viewModel.Message = $"{key} зарезервирована. Выберите другую кнопку или нажмите Esc.";
            return;
        }
        PopulateStartKeys(key);
        _viewModel.Message = $"Выбрано: {GlobalHotkeyService.DisplayName(key)}. Укажите число нажатий и примените.";
    }
    private void CheckUpdatesOnStartup(object? sender, EventArgs e)
    {
        ContentRendered -= CheckUpdatesOnStartup;
        if (_viewModel.Settings.CheckUpdatesOnStartup) _ = CheckForUpdatesAsync(false);
    }

    private async void CheckUpdatesClick(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync(true);

    private bool _checkingUpdates;
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        if (manual) _viewModel.Message = "Проверяем обновления…";
        try
        {
            var installed = Assembly.GetEntryAssembly()!.GetName().Version!;
            var update = await ReleaseUpdates.FindAsync(installed);
            if (!IsVisible) return;
            if (update is { } available)
            {
                var choice = MessageBox.Show(this,
                    $"Доступна версия {available.Version}. Открыть страницу загрузки установщика?",
                    "Обновление ClickForge", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (choice == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(available.Page.AbsoluteUri) { UseShellExecute = true });
                if (manual) _viewModel.Message = $"Доступна версия {available.Version}.";
            }
            else if (manual) _viewModel.Message = "Новых опубликованных версий нет.";
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException
            or System.ComponentModel.Win32Exception)
        {
            if (manual) _viewModel.Message = "Не удалось проверить обновления. Попробуйте позже.";
        }
        finally { _checkingUpdates = false; }
    }
    private async void ApplyStartKey(object sender, RoutedEventArgs e)
    {
        if (StartKeyPicker.SelectedValue is not Key key || key == CustomKey || StartPressCountPicker.SelectedValue is not int count) return;
        var button = (Button)sender;
        button.IsEnabled = false;
        try
        {
            var oldKey = Enum.TryParse<Key>(_viewModel.StartHotkey, out var previous) ? previous : Key.F6;
            var oldCount = _viewModel.StartPressCount;
            _hotkeys.ChangeStartKey(key, count);
            try { await _viewModel.SaveStartHotkeyAsync(key.ToString(), count); }
            catch { _hotkeys.ChangeStartKey(oldKey, oldCount); throw; }
            _viewModel.Message = $"{GlobalHotkeyService.DisplayName(key)} × {count} — запуск / остановка. F10 — аварийная остановка.";
        }
        catch (Exception ex) { _viewModel.Message = ex.Message; }
        finally { button.IsEnabled = true; }
    }
    private async void OnHotkey(int id)
    {
        switch (id) { case 1: await _viewModel.ToggleFromHotkeyAsync(); break; case 2: _viewModel.PauseFromHotkey(); break; case 3: await _viewModel.EmergencyStopAsync(); break; }
    }
    protected override async void OnClosed(EventArgs e)
    {
        _hotkeys.Pressed -= OnHotkey; _hotkeys.Dispose();
        await _viewModel.EmergencyStopAsync();
        base.OnClosed(e);
    }
}
