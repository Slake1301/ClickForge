using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ClickForge.Core;
using CoreMouseButton = ClickForge.Core.MouseButton;

namespace ClickForge.UI;

public sealed class MainViewModel : ObservableObject
{
    private readonly IAutomationEngine _engine;
    private readonly IInputService _input;
    private readonly ISettingsStore _store;
    private readonly IStatisticsService _stats;
    private AppSettings _settings;
    private AutomationSnapshot _snapshot = new(AutomationState.Stopped, 0, 0, TimeSpan.Zero);
    private int _selectedSection;
    private string _message = "Готов к работе";

    public MainViewModel(IAutomationEngine engine, IInputService input, ISettingsStore store, IStatisticsService stats, AppSettings settings)
    {
        _engine = engine; _input = input; _store = store; _stats = stats; _settings = settings;
        StartStopCommand = new AsyncCommand(StartStopAsync);
        PauseCommand = new RelayCommand(PauseResume);
        CapturePositionCommand = new AsyncCommand(CapturePositionAsync);
        SaveCommand = new AsyncCommand(SaveAsync);
        StartSequenceCommand = new AsyncCommand(StartSequenceAsync);
        AddStepCommand = new RelayCommand(AddStep);
        RemoveStepCommand = new RelayCommand<MultiClickStep>(RemoveStep);
        ClearStatisticsCommand = new RelayCommand(() => { _stats.Clear(); Raise(nameof(TotalClicks)); Message = "Статистика очищена"; });
        _engine.SnapshotChanged += OnSnapshot;
    }

    public AppSettings Settings { get => _settings; private set { _settings = value; Raise(); Raise(nameof(Steps)); } }
    public ObservableCollection<MultiClickStep> Steps => Settings.MultiClickSteps;
    public Array MouseButtons => Enum.GetValues<CoreMouseButton>();
    public Array ClickTypes => Enum.GetValues<ClickType>();
    public Array PositionModes => Enum.GetValues<PositionMode>();
    public Array RepeatModes => Enum.GetValues<RepeatMode>();
    public AutomationSnapshot Snapshot { get => _snapshot; private set { _snapshot = value; Raise(); Raise(nameof(StatusText)); Raise(nameof(StartText)); } }
    public int SelectedSection { get => _selectedSection; set => Set(ref _selectedSection, value); }
    public string Message { get => _message; set => Set(ref _message, value); }
    public string StatusText => Snapshot.State switch { AutomationState.Running => "РАБОТАЕТ", AutomationState.Paused => "ПАУЗА", _ => "ОСТАНОВЛЕН" };
    public string StartText => Snapshot.State == AutomationState.Stopped ? "▶  ЗАПУСТИТЬ" : "■  ОСТАНОВИТЬ";
    public long TotalClicks => _stats.TotalClicks;
    public ICommand StartStopCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand CapturePositionCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand StartSequenceCommand { get; }
    public ICommand AddStepCommand { get; }
    public ICommand RemoveStepCommand { get; }
    public ICommand ClearStatisticsCommand { get; }
    public string StartHotkey => Settings.StartHotkey;
    public string StartGesture
    {
        get
        {
            var name = Enum.TryParse<Key>(StartHotkey, out var key)
                ? Hotkeys.GlobalHotkeyService.DisplayName(key) : StartHotkey;
            return StartPressCount == 2 ? $"{name} × 2" : name;
        }
    }
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _store.SaveAsync(settings);
        Settings = settings;
        Raise(nameof(StartHotkey));
        Raise(nameof(StartGesture));
    }
    public int StartPressCount => Settings.StartPressCount == 2 ? 2 : 1;
    public async Task SaveStartHotkeyAsync(string key, int count)
    {
        var oldKey = Settings.StartHotkey;
        var oldCount = Settings.StartPressCount;
        Settings.StartHotkey = key;
        Settings.StartPressCount = count;
        try { await _store.SaveAsync(Settings); }
        catch
        {
            Settings.StartHotkey = oldKey;
            Settings.StartPressCount = oldCount;
            throw;
        }
        Raise(nameof(StartHotkey));
        Raise(nameof(StartGesture));
    }

    public async Task ToggleFromHotkeyAsync() => await StartStopAsync();
    public void PauseFromHotkey() => PauseResume();
    public Task EmergencyStopAsync() { Message = "Экстренная остановка"; return _engine.StopAsync(); }

    private async Task StartStopAsync()
    {
        try
        {
            if (_engine.State != AutomationState.Stopped) await _engine.StopAsync();
            else { await _engine.StartClickerAsync(Settings.Clicker); Message = "Автоматизация запущена"; }
        }
        catch (Exception ex) { Message = ex.Message; }
    }
    private void PauseResume()
    {
        if (_engine.State == AutomationState.Running) _engine.Pause(); else if (_engine.State == AutomationState.Paused) _engine.Resume();
    }
    private async Task CapturePositionAsync()
    {
        try
        {
            for (int seconds = 3; seconds > 0; seconds--)
            {
                Message = $"Наведите курсор на цель. Захват через {seconds} сек.";
                await Task.Delay(1000);
            }
            var point = _input.GetCursorPosition();
            Settings.Clicker.X = point.X;
            Settings.Clicker.Y = point.Y;
            Settings.Clicker.PositionMode = PositionMode.Fixed;
            Message = $"Выбрана точка X: {point.X}, Y: {point.Y}";
        }
        catch (Exception ex) { Message = ex.Message; }
    }
    private async Task StartSequenceAsync()
    {
        try { if (_engine.State != AutomationState.Stopped) await _engine.StopAsync(); else await _engine.StartSequenceAsync(Steps.ToList(), 1); }
        catch (Exception ex) { Message = ex.Message; }
    }
    private void AddStep()
    {
        var p = _input.GetCursorPosition(); Steps.Add(new() { Name = $"Точка {Steps.Count + 1}", X = p.X, Y = p.Y });
    }
    private void RemoveStep(MultiClickStep? step) { if (step is not null) Steps.Remove(step); }
    private async Task SaveAsync() { await _store.SaveAsync(Settings); await _stats.SaveAsync(); Message = "Настройки сохранены"; }
    private void OnSnapshot(object? sender, AutomationSnapshot value)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Snapshot = value;
            Raise(nameof(TotalClicks));
            if (value.Error is not null) Message = "Ошибка: " + value.Error;
        });
    }
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
public sealed class RelayCommand<T>(Action<T?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter is T value ? value : default);
}
public sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running;
    public async void Execute(object? parameter)
    {
        if (_running) return; _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); } finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
