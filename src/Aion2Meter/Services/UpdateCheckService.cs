using System;
using System.Diagnostics;
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
    string? Error,
    UpdatePackage? Package = null);

internal sealed record UpdatePackage(
    Version Version,
    string InstallerName,
    Uri InstallerUrl,
    Uri ChecksumUrl,
    long InstallerSize);

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
            if (!IsTrustedReleaseUrl(url))
                url = null;
            if (!TryParseVersion(tag, out Version? latest))
            {
                return new UpdateCheckResult(false, false, current, null, url, "Invalid release version");
            }

            UpdatePackage? package = TryReadPackage(document.RootElement, tag!, latest!);
            bool newer = new Version(latest!.Major, latest.Minor, latest.Build) >
                         new Version(current.Major, current.Minor, Math.Max(0, current.Build));
            return new UpdateCheckResult(true, newer, current, latest, url, null, package);
        }
        catch (Exception exception)
        {
            return new UpdateCheckResult(false, false, current, null, null, exception.Message);
        }
    }

    public static void OpenRelease(string? url)
    {
        if (!IsTrustedReleaseUrl(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
        }
        catch
        {
            // A missing browser must not terminate the meter from a tray event.
        }
    }

    internal static bool IsTrustedReleaseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith("/CHAINSGANG/Aion2Meter/releases/", StringComparison.OrdinalIgnoreCase);

    internal static bool IsTrustedAssetUrl(string? url, string tag, string assetName) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.AbsolutePath,
            $"/CHAINSGANG/Aion2Meter/releases/download/{tag}/{assetName}",
            StringComparison.Ordinal);

    internal static UpdatePackage? TryReadPackage(JsonElement release, string tag, Version version)
    {
        string versionText = version.ToString(3);
        if (!string.Equals(tag, $"v{versionText}", StringComparison.Ordinal) ||
            !release.TryGetProperty("assets", out JsonElement assets) ||
            assets.ValueKind != JsonValueKind.Array)
            return null;

        string installerName = $"Aion2Meter-Setup-v{versionText}.exe";
        string checksumName = installerName + ".sha256";
        Uri? installerUrl = null;
        Uri? checksumUrl = null;
        long installerSize = 0;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out JsonElement nameElement) ||
                !asset.TryGetProperty("browser_download_url", out JsonElement urlElement))
                continue;

            string? name = nameElement.GetString();
            string? downloadUrl = urlElement.GetString();
            if (name is null || !IsTrustedAssetUrl(downloadUrl, tag, name))
                continue;

            if (name == installerName &&
                asset.TryGetProperty("size", out JsonElement sizeElement) &&
                sizeElement.TryGetInt64(out long size) &&
                size is > 0 and <= 1_073_741_824)
            {
                installerUrl = new Uri(downloadUrl!);
                installerSize = size;
            }
            else if (name == checksumName)
            {
                checksumUrl = new Uri(downloadUrl!);
            }
        }

        return installerUrl is not null && checksumUrl is not null
            ? new UpdatePackage(version, installerName, installerUrl, checksumUrl, installerSize)
            : null;
    }

    private static bool TryParseVersion(string? tag, out Version? version)
    {
        version = null;
        string normalized = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
        if (normalized.Split('.').Length != 3 || !Version.TryParse(normalized, out version))
            return false;
        return version.Build >= 0;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2Meter/update-check");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
