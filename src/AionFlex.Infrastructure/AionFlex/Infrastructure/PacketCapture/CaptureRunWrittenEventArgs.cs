using AionFlex.Core.Capture;

namespace AionFlex.Infrastructure.PacketCapture;

public sealed record CaptureRunWrittenEventArgs(CaptureRunIndex Index, string IndexPath);
