using System;
using System.Diagnostics;
using System.IO;

namespace Aion2Meter.Services;

internal static class NpcapServiceLifecycle
{
    private const int StopTimeoutMilliseconds = 5000;

    public static void TryStop(DiagnosticLog diagnostics)
    {
        try
        {
            string serviceController = Path.Combine(Environment.SystemDirectory, "sc.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = serviceController,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("stop");
            startInfo.ArgumentList.Add("npcap");

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                diagnostics.Write("NPCAP", "service stop could not be started");
                return;
            }

            if (!process.WaitForExit(StopTimeoutMilliseconds))
            {
                diagnostics.Write("NPCAP", "service stop timed out; Windows will finish the pending stop");
                return;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            string detail = string.IsNullOrWhiteSpace(error) ? output : error;
            diagnostics.Write(
                "NPCAP",
                process.ExitCode == 0
                    ? "service stop requested successfully"
                    : $"service stop returned {process.ExitCode}: {detail}");
        }
        catch (Exception ex)
        {
            diagnostics.Write("NPCAP", $"service stop failed: {ex.Message}");
        }
    }
}
