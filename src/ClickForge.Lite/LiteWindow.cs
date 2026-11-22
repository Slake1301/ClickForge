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
        [(MouseButton.Left, "Левая"), (MouseButton.Right, "Правая"), (MouseButton.Middle, "Средняя"),
         (MouseButton.X1, "Боковая · Назад"), (MouseButton.X2, "Боковая · Вперёд")];

    private readonly AutomationEngine _engine;
    private readonly JsonSettingsStore _store;
    private readonly ComboBox _mouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 86_400_000, Value = 1000 };
    private readonly NumericUpDown _quantity = new() { Minimum = 1, Maximum = 10_000_000, Value = 3 };
    private readonly CheckBox _continuous = new() { Text = "Повторять до остановки", AutoSize = true };
    private readonly Button _start = new() { Text = "Запустить", Enabled = false };
    private readonly Button _stop = new() { Text = "Остановить", Enabled = false };
    private readonly Label _status = new() { Text = "Загрузка настроек…", AutoSize = true };
    private readonly Label _shortcuts = new() { AutoSize = true };
    private readonly Button _assign = new() { Text = "Назначить кнопку…", AutoSize = true };
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

    public LiteWindow()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickForge Lite");
        _store = new JsonSettingsStore(directory);
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
        _presses.Items.AddRange(["1 — одиночное", "2 — двойное"]);
        _presses.SelectedIndex = 0;
        _presses.SelectedIndexChanged += (_, _) => { if (_ready) ApplyShortcut(_shortcutKey); };
        _assign.Click += (_, _) => CaptureShortcut();
        _continuous.CheckedChanged += (_, _) => _quantity.Enabled = !_continuous.Checked;
        _start.Click += async (_, _) => await StartOrStopAsync(fromButton: true);
        _stop.Click += async (_, _) => await StopAsync();
        Controls.Add(CreateLayout());
    }

    private Control CreateLayout()
    {
        var grid = new TableLayoutPanel { AutoSize = true, Padding = new Padding(18), ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 255));
        var title = new Label { Text = "ClickForge Lite", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        AddWide(title, 0);
        AddWide(new Label { Text = "Одиночные клики в текущей позиции курсора", AutoSize = true, Margin = new Padding(3, 4, 3, 12) }, 1);
        AddField("Кнопка мыши", _mouse, 2);
        AddField("Интервал, мс", _interval, 3);
        AddField("Число кликов", _quantity, 4);
        AddWide(_continuous, 5);
        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 6) };
        _start.AutoSize = _stop.AutoSize = true;
        _start.BackColor = Color.FromArgb(86, 62, 194);
        _start.ForeColor = Color.White;
        _start.FlatStyle = FlatStyle.Flat;
        actions.Controls.AddRange([_start, _stop]);
        AddField("Кнопка запуска", _assign, 6);
        AddField("Число нажатий", _presses, 7);
        AddWide(actions, 8);
        AddWide(_shortcuts, 9);
        _status.MaximumSize = new Size(390, 0);
        _status.Margin = new Padding(3, 6, 3, 0);
        AddWide(_status, 10);
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

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (File.Exists(Path.Combine(data, "ClickForge Lite", "settings.json")))
            {
                _settings = await _store.LoadAsync();
                var click = _settings.Clicker;
                _mouse.SelectedIndex = Math.Max(0, Array.FindIndex(Buttons, button => button.Value == click.Button));
                _interval.Value = click.IntervalMs;
                _quantity.Value = click.RepeatCount;
                _continuous.Checked = click.RepeatMode == RepeatMode.UntilStopped;
                if (_settings.StartHotkey.StartsWith("vk:", StringComparison.Ordinal) &&
                    int.TryParse(_settings.StartHotkey.AsSpan(3), out var storedKey) && storedKey is > 0 and < 255
                    && storedKey != (int)Keys.F11) _shortcutKey = storedKey;
                _presses.SelectedIndex = _settings.StartPressCount == 2 ? 1 : 0;
            }
            _status.Text = "Готов к работе";
        }
        catch (Exception ex) { _status.Text = $"Настройки не загружены: {ex.Message}"; }
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
        _assign.Text = LiteShortcut.Name(_shortcutKey) + " · изменить";
        string shortcut = _shortcutReady ? $"{LiteShortcut.Name(_shortcutKey)} × {_presses.SelectedIndex + 1} — запуск / стоп" : "Горячая кнопка недоступна";
        _shortcuts.Text = $"{shortcut}   ·   {(_stopRegistered ? "F11 — аварийный стоп" : "F11 занята")}";
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
                _status.Text = "Старт через 3 сек. Наведите курсор на цель.";
                _start.Text = "Отменить";
                _stop.Enabled = true;
                try { await Task.Delay(3000, delay.Token); }
                catch (OperationCanceledException)
                {
                    _status.Text = "Запуск отменён";
                    return;
                }
                finally
                {
                    _startDelay = null;
                    _start.Text = "Запустить";
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
            _start.Text = running ? "Стоп" : "Запустить";
            _stop.Enabled = running;
            _status.Text = snapshot.Error ?? $"{(running ? "Работает" : "Остановлен")} · Кликов: {snapshot.Clicks}";
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
