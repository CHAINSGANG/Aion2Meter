namespace AionFlex.Core.Models;

public sealed record DownloadProgress(long BytesReceived, long? TotalBytes, int Percent, double BytesPerSecond);
