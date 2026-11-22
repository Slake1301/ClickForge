namespace ClickForge.Core;

public interface IInputService
{
    (int X, int Y) GetCursorPosition();
    void MoveCursor(int x, int y);
    void MouseDown(MouseButton button);
    void MouseUp(MouseButton button);
    Task ClickAsync(MouseButton button, ClickType type, int holdDurationMs, CancellationToken token);
    void ReleaseAll();
}

public interface IAutomationEngine
{
    AutomationState State { get; }
    event EventHandler<AutomationSnapshot>? SnapshotChanged;
    Task StartClickerAsync(ClickSettings settings);
    Task StartSequenceAsync(IReadOnlyList<MultiClickStep> steps, int cycles);
    void Pause();
    void Resume();
    Task StopAsync();
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken token = default);
    Task SaveAsync(AppSettings settings, CancellationToken token = default);
    Task ExportAsync(AppSettings settings, string destination, CancellationToken token = default);
    Task<AppSettings> ImportAsync(string source, CancellationToken token = default);
}

public interface IStatisticsService
{
    long SessionClicks { get; }
    long TotalClicks { get; }
    void RecordClick();
    Task LoadAsync();
    Task SaveAsync();
    void Clear();
}
