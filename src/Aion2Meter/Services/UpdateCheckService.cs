using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aion2Meter.Services;

internal sealed record UpdateCheckResult(
    bool Succeeded,
    bool IsUpdateAvailable,
    Version CurrentVersion,
    Version? LatestVersion,
    string? ReleaseUrl,
    string? Error);

internal static class UpdateCheckService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/CHAINSGANG/Aion2Meter/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        Version current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
        try
        {
            using HttpResponseMessage response = await Client.GetAsync(LatestReleaseApi).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            string? tag = document.RootElement.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString()
                : null;
            string? url = document.RootElement.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null;
            if (!TryParseVersion(tag, out Version? latest))
            {
                return new UpdateCheckResult(false, false, current, null, url, "Invalid release version");
            }

            return new UpdateCheckResult(true, latest > current, current, latest, url, null);
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(false, false, current, null, null, exception.Message);
        }
    }

    public static void OpenRelease(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static bool TryParseVersion(string? tag, out Version? version)
    {
        string normalized = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out version);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2Meter/update-check");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
