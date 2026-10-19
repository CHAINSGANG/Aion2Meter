using System;
using System.Diagnostics;
using System.IO;

namespace Aion2Meter.Services;

internal static class NpcapServiceLifecycle
{
    private const int ControlTimeoutMilliseconds = 5000;

    public static void TryStart(DiagnosticLog diagnostics) =>
        TryControl("start", diagnostics);

    public static void TryStop(DiagnosticLog diagnostics) =>
        TryControl("stop", diagnostics);

    private static void TryControl(string command, DiagnosticLog diagnostics)
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
            startInfo.ArgumentList.Add(command);
            startInfo.ArgumentList.Add("npcap");

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                diagnostics.Write("NPCAP", $"service {command} could not be started");
                return;
            }

            if (!process.WaitForExit(ControlTimeoutMilliseconds))
            {
                diagnostics.Write("NPCAP", $"service {command} timed out; Windows will finish the pending operation");
                return;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            string detail = string.IsNullOrWhiteSpace(error) ? output : error;
            diagnostics.Write(
                "NPCAP",
                process.ExitCode == 0
                    ? $"service {command} requested successfully"
                    : $"service {command} returned {process.ExitCode}: {detail}");
        }
        catch (Exception ex)
        {
            diagnostics.Write("NPCAP", $"service {command} failed: {ex.Message}");
        }
    }
}
