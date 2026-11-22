using System.Text.Json;
using System.Text.Json.Serialization;
using ClickForge.Core;

namespace ClickForge.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public JsonSettingsStore(string dataDirectory) => _path = Path.Combine(dataDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(_path)) return CreateDefaults();
        try
        {
            await using var stream = File.OpenRead(_path);
            var result = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _json, token);
            return result is { SchemaVersion: 1 } ? result : CreateDefaults();
        }
        catch (JsonException) { return CreateDefaults(); }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, settings, _json, token);
        File.Move(temporary, _path, true);
    }

    public async Task ExportAsync(AppSettings settings, string destination, CancellationToken token = default)
    {
        await using var stream = File.Create(destination);
        await JsonSerializer.SerializeAsync(stream, settings, _json, token);
    }

    public async Task<AppSettings> ImportAsync(string source, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(source);
        var result = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _json, token) ?? throw new InvalidDataException("Файл конфигурации пуст.");
        if (result.SchemaVersion != 1) throw new InvalidDataException("Неподдерживаемая версия конфигурации.");
        return result;
    }

    public static AppSettings CreateDefaults() => new()
    {
        MultiClickSteps =
        [
            new() { Name = "Точка A", X = 100, Y = 100, DelayAfterMs = 500 },
            new() { Name = "Точка B", X = 300, Y = 200, ClickType = ClickType.Double, DelayAfterMs = 1000 }
        ]
    };
}
