using System;

namespace AionFlex.Core.Models;

public sealed record DownloadResult(string FilePath, long BytesDownloaded, TimeSpan Elapsed);
