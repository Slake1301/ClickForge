using System.Diagnostics;
using ClickForge.Core;

namespace ClickForge.Automation;

public sealed class AutomationEngine : IAutomationEngine, IDisposable
{
    private readonly IInputService _input;
    private readonly IStatisticsService _stats;
    private readonly SemaphoreSlim _singleRun = new(1, 1);
    private readonly ManualResetEventSlim _pause = new(true);
    private CancellationTokenSource? _cts;
    private Task? _run;
    private long _clicks;
    private Stopwatch? _clock;
    private long _lastProgress;
    public AutomationState State { get; private set; } = AutomationState.Stopped;
    public event EventHandler<AutomationSnapshot>? SnapshotChanged;

    public AutomationEngine(IInputService input, IStatisticsService stats) { _input = input; _stats = stats; }

    public Task StartClickerAsync(ClickSettings settings)
    {
        settings.Validate();
        return StartAsync(token => RunClickerAsync(settings, token));
    }

    public Task StartSequenceAsync(IReadOnlyList<MultiClickStep> steps, int cycles)
    {
        if (steps.Count == 0) throw new InvalidOperationException(UiText.Get("Добавьте хотя бы одну точку."));
        if (cycles < 1) throw new ArgumentOutOfRangeException(nameof(cycles));
        return StartAsync(token => RunSequenceAsync(steps, cycles, token));
    }

    private Task StartAsync(Func<CancellationToken, Task> action)
    {
        if (!_singleRun.Wait(0)) throw new InvalidOperationException(UiText.Get("Другой сценарий уже выполняется."));
        _cts = new CancellationTokenSource();
        _clicks = 0;
        _clock = Stopwatch.StartNew();
        _lastProgress = Stopwatch.GetTimestamp();
        _pause.Set();
        State = AutomationState.Running;
        Publish();
        _run = Task.Run(async () =>
        {
            string? error = null;
            try { await action(_cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
            catch (Exception ex) { error = ex.Message; }
            finally
            {
                _input.ReleaseAll();
                _clock?.Stop();
                State = AutomationState.Stopped;
                Publish(error);
                _singleRun.Release();
            }
        });
        return Task.CompletedTask;
    }

    private async Task RunClickerAsync(ClickSettings settings, CancellationToken token)
    {
        var started = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            _pause.Wait(token);
            if (settings.RepeatMode == RepeatMode.Count && _clicks >= settings.RepeatCount) break;
            if (settings.RepeatMode == RepeatMode.Duration && started.Elapsed >= TimeSpan.FromSeconds(settings.DurationSeconds)) break;
            if (settings.PositionMode == PositionMode.Fixed) _input.MoveCursor(settings.X, settings.Y);
            await _input.ClickAsync(settings.Button, settings.ClickType, settings.HoldDurationMs, token).ConfigureAwait(false);
            RecordClick();
            var delay = settings.UseRandomInterval ? Random.Shared.Next(settings.IntervalMs, settings.RandomMaxMs + 1) : settings.IntervalMs;
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }

    private async Task RunSequenceAsync(IReadOnlyList<MultiClickStep> steps, int cycles, CancellationToken token)
    {
        for (var cycle = 0; cycle < cycles; cycle++)
        foreach (var step in steps.Where(s => s.Enabled))
        {
            _pause.Wait(token);
            await Task.Delay(Math.Max(0, step.DelayBeforeMs), token).ConfigureAwait(false);
            _input.MoveCursor(step.X, step.Y);
            for (var i = 0; i < Math.Max(1, step.RepeatCount); i++)
            {
                await _input.ClickAsync(step.Button, step.ClickType, step.HoldDurationMs, token).ConfigureAwait(false);
                RecordClick();
            }
            await Task.Delay(Math.Max(0, step.DelayAfterMs), token).ConfigureAwait(false);
        }
    }

    private void RecordClick()
    {
        Interlocked.Increment(ref _clicks);
        _stats.RecordClick();
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_lastProgress, now) < TimeSpan.FromMilliseconds(100)) return;
        _lastProgress = now;
        Publish();
    }
    public void Pause() { if (State != AutomationState.Running) return; _pause.Reset(); State = AutomationState.Paused; Publish(); }
    public void Resume() { if (State != AutomationState.Paused) return; _pause.Set(); State = AutomationState.Running; Publish(); }
    public async Task StopAsync()
    {
        var run = _run;
        if (run is null) return;
        _cts?.Cancel();
        _pause.Set();
        await run.ConfigureAwait(false);
    }
    private void Publish(string? error = null)
    {
        var elapsed = _clock?.Elapsed ?? TimeSpan.Zero;
        SnapshotChanged?.Invoke(this, new(State, Interlocked.Read(ref _clicks), elapsed.TotalSeconds > 0 ? _clicks / elapsed.TotalSeconds : 0, elapsed, error));
    }
    public void Dispose() { _cts?.Cancel(); _pause.Set(); _input.ReleaseAll(); _pause.Dispose(); _singleRun.Dispose(); _cts?.Dispose(); }
}
