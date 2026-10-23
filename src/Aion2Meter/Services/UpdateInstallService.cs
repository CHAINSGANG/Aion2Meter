using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aion2Meter.Services;

internal static class UpdateInstallService
{
    private const long MaximumInstallerBytes = 1_073_741_824;
    private static readonly HttpClient Client = CreateClient();

    public static async Task<string> DownloadVerifiedInstallerAsync(
        UpdatePackage package,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string versionText = package.Version.ToString(3);
        if (package.InstallerName != $"Aion2Meter-Setup-v{versionText}.exe" ||
            !UpdateCheckService.IsTrustedAssetUrl(package.InstallerUrl.ToString(),
                $"v{versionText}", package.InstallerName) ||
            !UpdateCheckService.IsTrustedAssetUrl(package.ChecksumUrl.ToString(),
                $"v{versionText}", package.InstallerName + ".sha256"))
            throw new InvalidDataException("Release assets do not match the selected version.");

        if (package.InstallerSize is <= 0 or > MaximumInstallerBytes)
            throw new InvalidDataException("Release installer size is invalid.");

        string checksumText = await ReadChecksumAsync(package.ChecksumUrl, cancellationToken)
            .ConfigureAwait(false);
        if (!TryParseChecksum(checksumText, package.InstallerName, out string? expectedHash))
            throw new InvalidDataException("Release SHA-256 manifest is invalid.");

        string stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Aion2Meter-Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        string installerPath = Path.Combine(stagingDirectory, package.InstallerName);

        try
        {
            using HttpResponseMessage response = await Client.GetAsync(
                package.InstallerUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            EnsureTrustedDownloadHost(response);
            if (response.Content.Headers.ContentLength is long announcedSize &&
                announcedSize != package.InstallerSize)
                throw new InvalidDataException("Release installer size differs from GitHub metadata.");

            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var output = new FileStream(installerPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 131_072, useAsync: true);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[131_072];
            long received = 0;
            int lastReportedPercent = -1;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                received += read;
                if (received > package.InstallerSize || received > MaximumInstallerBytes)
                    throw new InvalidDataException("Release installer exceeds its advertised size.");

                hash.AppendData(buffer.AsSpan(0, read));
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                int percent = (int)(received * 100 / package.InstallerSize);
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress?.Report(percent);
                }
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (received != package.InstallerSize)
                throw new InvalidDataException("Release installer download is incomplete.");

            string actualHash = Convert.ToHexString(hash.GetHashAndReset());
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Release installer SHA-256 does not match the manifest.");

            return installerPath;
        }
        catch
        {
            try
            {
                if (File.Exists(installerPath))
                    File.Delete(installerPath);
                Directory.Delete(stagingDirectory, recursive: false);
            }
            catch (IOException) { /* Preserve the download error. */ }
            catch (UnauthorizedAccessException) { /* Preserve the download error. */ }
            throw;
        }
    }

    public static void LaunchInstaller(string installerPath, string applicationDirectory)
    {
        ProcessStartInfo startInfo = CreateInstallerStartInfo(installerPath, applicationDirectory);
        using Process? process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException("Windows did not start the update installer.");
    }

    internal static ProcessStartInfo CreateInstallerStartInfo(
        string installerPath,
        string applicationDirectory)
    {
        string executable = Path.GetFullPath(installerPath);
        string installDirectory = Path.GetFullPath(applicationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(executable) ||
            !File.Exists(Path.Combine(installDirectory, "Aion2Meter.exe")) ||
            string.Equals(installDirectory, Path.GetPathRoot(installDirectory)?.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The current meter installation directory is invalid.");

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        startInfo.ArgumentList.Add($"/DIR={installDirectory}");
        return startInfo;
    }

    internal static bool TryParseChecksum(string text, string expectedFileName, out string? hash)
    {
        hash = null;
        Match match = Regex.Match(text.Trim(),
            @"\A(?<hash>[0-9A-Fa-f]{64})  (?<file>[A-Za-z0-9._-]+)\z",
            RegexOptions.CultureInvariant);
        if (!match.Success ||
            !string.Equals(match.Groups["file"].Value, expectedFileName, StringComparison.Ordinal))
            return false;

        hash = match.Groups["hash"].Value;
        return true;
    }

    private static async Task<string> ReadChecksumAsync(Uri url, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await Client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        EnsureTrustedDownloadHost(response);
        if (response.Content.Headers.ContentLength is > 4096)
            throw new InvalidDataException("Release SHA-256 manifest is too large.");

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] buffer = new byte[4097];
        int total = 0;
        int read;
        while (total < buffer.Length &&
               (read = await input.ReadAsync(buffer.AsMemory(total), cancellationToken)
                   .ConfigureAwait(false)) > 0)
            total += read;
        if (total > 4096)
            throw new InvalidDataException("Release SHA-256 manifest is too large.");
        return Encoding.ASCII.GetString(buffer, 0, total);
    }

    private static void EnsureTrustedDownloadHost(HttpResponseMessage response)
    {
        Uri? finalUri = response.RequestMessage?.RequestUri;
        if (finalUri?.Scheme != Uri.UriSchemeHttps ||
            (finalUri.Host != "github.com" &&
             finalUri.Host != "release-assets.githubusercontent.com" &&
             finalUri.Host != "objects.githubusercontent.com"))
            throw new InvalidDataException("Release asset redirected to an untrusted host.");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aion2Meter/update-installer");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
        return client;
    }
}
