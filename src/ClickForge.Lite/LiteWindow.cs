using System.Runtime.InteropServices;
using ClickForge.Automation;
using ClickForge.Core;
using ClickForge.Diagnostics;
using ClickForge.Input;
using ClickForge.Storage;

namespace ClickForge.Lite;

internal sealed class LiteWindow : Form
{
    private static readonly (MouseButton Value, string Label)[] Buttons =
        [(MouseButton.Left, UiText.Get("Левая")), (MouseButton.Right, UiText.Get("Правая")), (MouseButton.Middle, UiText.Get("Средняя")),
         (MouseButton.X1, UiText.Get("Боковая · Назад")), (MouseButton.X2, UiText.Get("Боковая · Вперёд"))];

    private readonly AutomationEngine _engine;
    private readonly JsonSettingsStore _store;
    private readonly ComboBox _mouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 86_400_000, Value = 1000 };
    private readonly NumericUpDown _quantity = new() { Minimum = 1, Maximum = 10_000_000, Value = 3 };
    private readonly CheckBox _continuous = new() { Text = UiText.Get("Повторять до остановки"), AutoSize = true };
    private readonly Button _start = new() { Text = UiText.Get("Запустить"), Enabled = false };
    private readonly Button _stop = new() { Text = UiText.Get("Остановить"), Enabled = false };
    private readonly Label _status = new() { Text = UiText.Get("Загрузка настроек…"), AutoSize = true };
    private readonly Label _shortcuts = new() { AutoSize = true };
    private readonly Button _assign = new() { Text = UiText.Get("Назначить кнопку…"), AutoSize = true };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _languageNotice = new() { AutoSize = true, MaximumSize = new Size(390, 0) };
    private readonly ComboBox _presses = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly LiteShortcut _shortcut;
    private AppSettings _settings = new();
    private bool _ready;
    private bool _closing;
    private bool _closed;
    private bool _capturingShortcut;
    private bool _shortcutReady;
    private int _shortcutKey = (int)Keys.F8;
    private bool _stopRegistered;
    private Icon? _ownedIcon;
    private CancellationTokenSource? _startDelay;

    public LiteWindow(string directory, JsonSettingsStore store, AppSettings? settings, string? loadError)
    {
        _store = store;
        if (settings is not null) _settings = settings;
        _engine = new AutomationEngine(new WindowsInputService(), new StatisticsService(directory));
        _shortcut = new LiteShortcut(() =>
        {
            if (!_capturingShortcut && _ready && !_closing)
                BeginInvoke(async () => { if (!_capturingShortcut) await StartOrStopAsync(); });
        });
        _engine.SnapshotChanged += OnSnapshot;
        Text = $"ClickForge Lite {typeof(LiteWindow).Assembly.GetName().Version!.ToString(3)}";
        _ownedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        if (_ownedIcon is not null) Icon = _ownedIcon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 246, 251);
        ForeColor = Color.FromArgb(30, 35, 54);
        _mouse.Items.AddRange(Buttons.Select(button => button.Label).Cast<object>().ToArray());
        _mouse.SelectedIndex = 0;
        _presses.Items.AddRange([UiText.Get("1 — одиночное"), UiText.Get("2 — двойное")]);
        _presses.SelectedIndex = 0;
        _presses.SelectedIndexChanged += (_, _) => { if (_ready) ApplyShortcut(_shortcutKey); };
        _assign.Click += (_, _) => CaptureShortcut();
        _continuous.CheckedChanged += (_, _) => _quantity.Enabled = !_continuous.Checked;
        _start.Click += async (_, _) => await StartOrStopAsync(fromButton: true);
        _stop.Click += async (_, _) => await StopAsync();
        _language.Items.AddRange(["Русский", "English"]);
        _language.SelectedIndex = UiText.Language == "en" ? 1 : 0;
        _language.SelectedIndexChanged += (_, _) =>
        {
            _settings.Language = _language.SelectedIndex == 1 ? "en" : "ru";
            _languageNotice.Text = _settings.Language == UiText.Language ? "" :
                UiText.Get("Язык сохранён. Перезапустите ClickForge, чтобы применить его.");
        };
        Controls.Add(CreateLayout());
        if (settings is not null) LoadControls(settings);
        _status.Text = loadError is null ? UiText.Get("Готов к работе") :
            UiText.Get("Настройки не загружены: {0}", loadError);
    }

    private Control CreateLayout()
    {
        var grid = new TableLayoutPanel { AutoSize = true, Padding = new Padding(18), ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 255));
        var title = new Label { Text = "ClickForge Lite", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        AddWide(title, 0);
        AddWide(new Label { Text = UiText.Get("Одиночные клики в текущей позиции курсора"), AutoSize = true, Margin = new Padding(3, 4, 3, 12) }, 1);
        AddField(UiText.Get("Кнопка мыши"), _mouse, 2);
        AddField(UiText.Get("Интервал, мс"), _interval, 3);
        AddField(UiText.Get("Число кликов"), _quantity, 4);
        AddWide(_continuous, 5);
        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 6) };
        _start.AutoSize = _stop.AutoSize = true;
        _start.BackColor = Color.FromArgb(86, 62, 194);
        _start.ForeColor = Color.White;
        _start.FlatStyle = FlatStyle.Flat;
        actions.Controls.AddRange([_start, _stop]);
        AddField(UiText.Get("Кнопка запуска"), _assign, 6);
        AddField(UiText.Get("Число нажатий"), _presses, 7);
        AddWide(actions, 8);
        AddWide(_shortcuts, 9);
        _status.MaximumSize = new Size(390, 0);
        _status.Margin = new Padding(3, 6, 3, 0);
        AddWide(_status, 10);
        AddField(UiText.Get("Язык / Language"), _language, 11);
        AddWide(_languageNotice, 12);
        return grid;

        void AddWide(Control control, int row)
        {
            grid.Controls.Add(control, 0, row);
            grid.SetColumnSpan(control, 2);
        }
        void AddField(string label, Control control, int row)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, row);
            control.Dock = DockStyle.Fill;
            control.AccessibleName = label;
            grid.Controls.Add(control, 1, row);
        }
    }

    private void LoadControls(AppSettings settings)
    {
        var click = settings.Clicker;
        _mouse.SelectedIndex = Math.Max(0, Array.FindIndex(Buttons, button => button.Value == click.Button));
        _interval.Value = click.IntervalMs;
        _quantity.Value = click.RepeatCount;
        _continuous.Checked = click.RepeatMode == RepeatMode.UntilStopped;
        if (settings.StartHotkey.StartsWith("vk:", StringComparison.Ordinal) &&
            int.TryParse(settings.StartHotkey.AsSpan(3), out var storedKey) && storedKey is > 0 and < 255
            && storedKey != (int)Keys.F11) _shortcutKey = storedKey;
        _presses.SelectedIndex = settings.StartPressCount == 2 ? 1 : 0;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_closing || IsDisposed) return;
        _stopRegistered = RegisterHotKey(Handle, 2, 0x4000, (uint)Keys.F11);
        ApplyShortcut(_shortcutKey);
        _ready = _start.Enabled = true;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && _ready && !_closing)
        {
            if (message.WParam.ToInt32() == 2) BeginInvoke(async () => await StopAsync());
            return;
        }
        base.WndProc(ref message);
    }

    private void ApplyShortcut(int key)
    {
        try
        {
            _shortcut.Configure(key, _presses.SelectedIndex + 1);
            _shortcutKey = key;
            _shortcutReady = true;
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        _assign.Text = LiteShortcut.Name(_shortcutKey) + UiText.Get(" · изменить");
        string shortcut = _shortcutReady ? UiText.Get("{0} × {1} — запуск / стоп", LiteShortcut.Name(_shortcutKey), _presses.SelectedIndex + 1) : UiText.Get("Горячая кнопка недоступна");
        _shortcuts.Text = shortcut + "   ·   " + UiText.Get(_stopRegistered ? "F11 — аварийный стоп" : "F11 занята");
    }

    private void CaptureShortcut()
    {
        _capturingShortcut = true;
        try
        {
            using var dialog = new ShortcutPicker();
            if (dialog.ShowDialog(this) == DialogResult.OK) ApplyShortcut(dialog.SelectedKey);
        }
        finally { _capturingShortcut = false; }
    }

    private ClickSettings ReadControls() => new()
    {
        Button = Buttons[Math.Max(0, _mouse.SelectedIndex)].Value,
        IntervalMs = (int)_interval.Value,
        HoldDurationMs = 1,
        RepeatCount = (int)_quantity.Value,
        RepeatMode = _continuous.Checked ? RepeatMode.UntilStopped : RepeatMode.Count
    };

    private async Task StartOrStopAsync(bool fromButton = false)
    {
        if (!_ready || _closing) return;
        try
        {
            if (_engine.State != AutomationState.Stopped || _startDelay is not null)
            {
                await StopAsync();
                return;
            }
            var settings = ReadControls();
            if (fromButton)
            {
                using var delay = new CancellationTokenSource();
                _startDelay = delay;
                _status.Text = UiText.Get("Старт через 3 сек. Наведите курсор на цель.");
                _start.Text = UiText.Get("Отменить");
                _stop.Enabled = true;
                try { await Task.Delay(3000, delay.Token); }
                catch (OperationCanceledException)
                {
                    _status.Text = UiText.Get("Запуск отменён");
                    return;
                }
                finally
                {
                    _startDelay = null;
                    _start.Text = UiText.Get("Запустить");
                    _stop.Enabled = false;
                }
                if (_closing) return;
            }
            await _engine.StartClickerAsync(settings);
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private Task StopAsync()
    {
        _startDelay?.Cancel();
        return _engine.StopAsync();
    }

    private void OnSnapshot(object? sender, AutomationSnapshot snapshot)
    {
        if (_closing || !IsHandleCreated || IsDisposed) return;
        BeginInvoke(() =>
        {
            if (_closing) return;
            bool running = snapshot.State != AutomationState.Stopped;
            _start.Text = running ? UiText.Get("Стоп") : UiText.Get("Запустить");
            _stop.Enabled = running;
            _status.Text = snapshot.Error ?? UiText.Get("{0} · Кликов: {1}", UiText.Get(running ? "Работает" : "Остановлен"), snapshot.Clicks);
        });
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel || _closed) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _shortcut.Dispose();
        if (_stopRegistered) UnregisterHotKey(Handle, 2);
        try
        {
            await StopAsync();
            if (_ready)
            {
                _settings.Clicker = ReadControls();
                _settings.StartHotkey = $"vk:{_shortcutKey}";
                _settings.StartPressCount = _presses.SelectedIndex + 1;
                await _store.SaveAsync(_settings);
            }
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "ClickForge Lite"); }
        finally
        {
            _engine.SnapshotChanged -= OnSnapshot;
            _engine.Dispose();
            _closed = true;
            BeginInvoke(Close);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ownedIcon?.Dispose();
        base.OnFormClosed(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
