using System.Text.Json;
using ClickForge.Core;

namespace ClickForge.Diagnostics;

public sealed class StatisticsService : IStatisticsService
{
    private long _session;
    private long _total;
    private readonly string _path;
    public StatisticsService(string dataDirectory) => _path = Path.Combine(dataDirectory, "statistics.json");
    public long SessionClicks => Interlocked.Read(ref _session);
    public long TotalClicks => Interlocked.Read(ref _total);
    public void RecordClick() { Interlocked.Increment(ref _session); Interlocked.Increment(ref _total); }
    public async Task LoadAsync()
    {
        if (!File.Exists(_path)) return;
        try { _total = (await JsonSerializer.DeserializeAsync<Stats>(File.OpenRead(_path)))?.TotalClicks ?? 0; } catch { _total = 0; }
    }
    public async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, new Stats(_total), new JsonSerializerOptions { WriteIndented = true });
    }
    public void Clear() { Interlocked.Exchange(ref _session, 0); Interlocked.Exchange(ref _total, 0); }
    private sealed record Stats(long TotalClicks);
}
