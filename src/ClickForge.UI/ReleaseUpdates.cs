using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace ClickForge.UI;

internal static class ReleaseUpdates
{
    private const string Repository = "Slake1301/ClickForge";
    private const string InstallerName = "ClickForge-Setup-x64.exe";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<(Version Version, Uri Page)?> FindAsync(Version installed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("ClickForge");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var release = json.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString();
        if (tag is null || !tag.StartsWith('v') || !Version.TryParse(tag[1..], out var version)) return null;
        var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
        if (version.Revision >= 0 || version.Build < 0 || version <= current) return null;
        var hasInstaller = release.GetProperty("assets").EnumerateArray().Any(asset =>
            asset.GetProperty("name").GetString() == InstallerName && asset.GetProperty("state").GetString() == "uploaded");
        return hasInstaller ? (version, new Uri($"https://github.com/{Repository}/releases/tag/v{version}")) : null;
    }
}
