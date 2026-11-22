using ClickForge.Automation;
using ClickForge.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("validation rejects zero interval", () => { var s = new ClickSettings(); typeof(ClickSettings).GetField("_intervalMs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(s, 0); AssertThrows<InvalidOperationException>(s.Validate); return Task.CompletedTask; }),
    ("feedback forms and theme contrast", FeedbackTests.RunAsync),
    ("count mode executes exact number", CountMode),
    ("stop releases held buttons", StopReleases),
    ("random interval bounds are validated", () => { var s = new ClickSettings { IntervalMs = 100, RandomMaxMs = 150 }; s.Validate(); return Task.CompletedTask; })
};
var failures = 0;
foreach (var test in tests) try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); } catch (Exception ex) { failures++; Console.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures;

static async Task CountMode()
{
    var input = new FakeInput(); var stats = new FakeStats(); using var engine = new AutomationEngine(input, stats);
    await engine.StartClickerAsync(new ClickSettings { RepeatMode = RepeatMode.Count, RepeatCount = 3, IntervalMs = 1, HoldDurationMs = 1 });
    await WaitUntil(() => engine.State == AutomationState.Stopped);
    if (input.Clicks != 3) throw new Exception($"expected 3, got {input.Clicks}");
}
static async Task StopReleases()
{
    var input = new FakeInput(); using var engine = new AutomationEngine(input, new FakeStats());
    await engine.StartClickerAsync(new ClickSettings { ClickType = ClickType.Hold, IntervalMs = 100 });
    await WaitUntil(() => input.Clicks > 0); await engine.StopAsync();
    if (input.ReleaseCalls == 0) throw new Exception("ReleaseAll was not called");
}
static async Task WaitUntil(Func<bool> predicate)
{
    using var timeout = new CancellationTokenSource(2000);
    while (!predicate()) { timeout.Token.ThrowIfCancellationRequested(); await Task.Delay(5); }
}
static void AssertThrows<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"expected {typeof(T).Name}"); }

sealed class FakeInput : IInputService
{
    public int Clicks; public int ReleaseCalls;
    public Task ClickAsync(MouseButton button, ClickType type, int holdDurationMs, CancellationToken token) { Interlocked.Increment(ref Clicks); return Task.CompletedTask; }
    public (int X, int Y) GetCursorPosition() => (0, 0); public void MoveCursor(int x, int y) { } public void MouseDown(MouseButton button) { } public void MouseUp(MouseButton button) { } public void ReleaseAll() => ReleaseCalls++;
}
sealed class FakeStats : IStatisticsService
{
    public long SessionClicks { get; private set; } public long TotalClicks => SessionClicks;
    public void RecordClick() => SessionClicks++; public Task LoadAsync() => Task.CompletedTask; public Task SaveAsync() => Task.CompletedTask; public void Clear() => SessionClicks = 0;
}
