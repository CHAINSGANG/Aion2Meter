using System.Collections.Generic;

namespace AionFlex.Infrastructure.PacketCapture;

internal static class CaptureWatchdog
{
	public const int FirstCheckSeconds = 180;

	public const int RepeatSeconds = 600;

	public const int MaxErrorsPerSession = 6;

	public static bool ShouldFire(long payloadsSinceStart, int errorsAlreadyLogged)
	{
		if (payloadsSinceStart == 0L)
		{
			return errorsAlreadyLogged < 6;
		}
		return false;
	}

	public static string ComposeBlindCaptureError(int elapsedSeconds, string captureMode, IReadOnlyList<string> deviceSummaries)
	{
		return $"\ud83d\udea8 Capture watchdog: 0 game payloads after {elapsedSeconds}s with AION 2 running — the meter is not seeing game traffic. mode={captureMode}, devices=[{string.Join("; ", deviceSummaries)}]. " + "Likely causes: game traffic on an adapter that is not being captured (loopback-only mode without a loopback proxy such as ExitLag), a VPN/accelerator adapter, or an Npcap driver problem.";
	}
}
