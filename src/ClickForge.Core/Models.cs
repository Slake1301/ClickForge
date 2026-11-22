using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClickForge.Core;

public enum MouseButton { Left, Right, Middle, X1, X2 }
public enum ClickType { Single, Double, Triple, Hold, Release }
public enum PositionMode { CurrentCursor, Fixed }
public enum RepeatMode { UntilStopped, Count, Duration }
public enum AutomationState { Stopped, Running, Paused }

public sealed class ClickSettings : ObservableObject
{
    private MouseButton _button = MouseButton.Left;
    private ClickType _clickType = ClickType.Single;
    private PositionMode _positionMode = PositionMode.CurrentCursor;
    private RepeatMode _repeatMode = RepeatMode.UntilStopped;
    private int _intervalMs = 100;
    private int _randomMaxMs = 100;
    private int _repeatCount = 100;
    private int _durationSeconds = 60;
    private int _holdDurationMs = 50;
    private int _x;
    private int _y;

    public MouseButton Button { get => _button; set => Set(ref _button, value); }
    public ClickType ClickType { get => _clickType; set => Set(ref _clickType, value); }
    public PositionMode PositionMode { get => _positionMode; set => Set(ref _positionMode, value); }
    public RepeatMode RepeatMode { get => _repeatMode; set => Set(ref _repeatMode, value); }
    public int IntervalMs { get => _intervalMs; set => Set(ref _intervalMs, Math.Clamp(value, 1, 86_400_000)); }
    public int RandomMaxMs { get => _randomMaxMs; set => Set(ref _randomMaxMs, Math.Max(value, IntervalMs)); }
    public int RepeatCount { get => _repeatCount; set => Set(ref _repeatCount, Math.Clamp(value, 1, 10_000_000)); }
    public int DurationSeconds { get => _durationSeconds; set => Set(ref _durationSeconds, Math.Clamp(value, 1, 604_800)); }
    public int HoldDurationMs { get => _holdDurationMs; set => Set(ref _holdDurationMs, Math.Clamp(value, 1, 60_000)); }
    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public bool UseRandomInterval { get; set; }

    public void Validate()
    {
        if (IntervalMs < 1) throw new InvalidOperationException(UiText.Get("Интервал должен быть не меньше 1 мс."));
        if (UseRandomInterval && RandomMaxMs < IntervalMs) throw new InvalidOperationException(UiText.Get("Максимальный интервал меньше минимального."));
        if (RepeatMode == RepeatMode.Count && RepeatCount < 1) throw new InvalidOperationException(UiText.Get("Число повторов должно быть положительным."));
    }
}

public sealed class MultiClickStep : ObservableObject
{
    private string _name = UiText.Get("Новая точка");
    private bool _enabled = true;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get => _name; set => Set(ref _name, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButton Button { get; set; } = MouseButton.Left;
    public ClickType ClickType { get; set; } = ClickType.Single;
    public int RepeatCount { get; set; } = 1;
    public int DelayBeforeMs { get; set; }
    public int DelayAfterMs { get; set; } = 100;
    public int HoldDurationMs { get; set; } = 50;
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string Language { get; set; } = "ru";
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "Violet";
    public string ActiveProfile { get; set; } = "По умолчанию";
    public bool SaveStatistics { get; set; } = true;
    public string StartHotkey { get; set; } = "F6";
    public int StartPressCount { get; set; } = 1;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public ClickSettings Clicker { get; set; } = new();
    public ObservableCollection<MultiClickStep> MultiClickSteps { get; set; } = [];
}

public sealed record AutomationSnapshot(AutomationState State, long Clicks, double ActualCps, TimeSpan Elapsed, string? Error = null);

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
