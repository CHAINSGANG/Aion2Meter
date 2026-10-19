namespace AionFlex.Core.Models;

public record UpdateCheckResult(bool IsAvailable, string LatestVersion, string? DownloadUrl, long? FileSizeBytes, string? Sha256, string? ReleaseNotes, string? ErrorMessage = null);
