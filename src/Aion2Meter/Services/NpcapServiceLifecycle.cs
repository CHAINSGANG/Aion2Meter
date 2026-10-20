using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Aion2Meter.Services;

internal static class NpcapServiceLifecycle
{
    private const int ControlTimeoutMilliseconds = 5000;
    private static readonly string[] ConcurrentCaptureProcesses =
        ["AionFlex", "Wireshark", "dumpcap", "tshark", "WinDump"];

    public static bool TryStart(DiagnosticLog diagnostics)
    {
        int state = QueryState(diagnostics);
        if (state == 4)
        {
            diagnostics.Write("NPCAP", "service already running; the meter will not own or stop it");
            return false;
        }

        if (state == 3)
        {
            throw new InvalidOperationException(
                "Npcap ещё завершает остановку (STOP_PENDING). Закройте программы захвата пакетов и дождитесь остановки драйвера перед запуском метра.");
        }

        return TryControl("start", diagnostics);
    }

    public static void TryStop(DiagnosticLog diagnostics, bool startedByMeter)
    {
        if (!startedByMeter)
        {
            diagnostics.Write("NPCAP", "service left running because the meter did not start it");
            return;
        }

        try
        {
            foreach (string processName in ConcurrentCaptureProcesses)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                try
                {
                    if (processes.Length > 0)
                    {
                        diagnostics.Write("NPCAP", $"service left running because {processName} is still using capture");
                        return;
                    }
                }
                finally
                {
                    foreach (Process process in processes)
                        process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics.Write("NPCAP", $"service left running because concurrent capture clients could not be checked: {ex.Message}");
            return;
        }

        int state = QueryState(diagnostics);
        if (state is 1 or 3)
        {
            diagnostics.Write("NPCAP", $"service stop skipped because its state is {state}");
            return;
        }

        TryControl("stop", diagnostics);
    }

    internal static bool IsKnownConcurrentCaptureProcess(string processName) =>
        Array.Exists(ConcurrentCaptureProcesses, name =>
            string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));

    private static int QueryState(DiagnosticLog diagnostics)
    {
        try
        {
            string serviceController = Path.Combine(Environment.SystemDirectory, "sc.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = serviceController,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add("query");
            startInfo.ArgumentList.Add("npcap");
            using Process? process = Process.Start(startInfo);
            if (process is null || !process.WaitForExit(ControlTimeoutMilliseconds))
                return 0;

            string output = process.StandardOutput.ReadToEnd();
            return process.ExitCode == 0 ? ParseServiceState(output) : 0;
        }
        catch (Exception ex)
        {
            diagnostics.Write("NPCAP", $"service state query failed: {ex.Message}");
            return 0;
        }
    }

    internal static int ParseServiceState(string output)
    {
        Match match = Regex.Match(output, @"\bSTATE\s*:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out int state) ? state : 0;
    }

    private static bool TryControl(string command, DiagnosticLog diagnostics)
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
                return false;
            }

            if (!process.WaitForExit(ControlTimeoutMilliseconds))
            {
                diagnostics.Write("NPCAP", $"service {command} timed out; Windows will finish the pending operation");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            string detail = string.IsNullOrWhiteSpace(error) ? output : error;
            diagnostics.Write(
                "NPCAP",
                process.ExitCode == 0
                    ? $"service {command} requested successfully"
                    : $"service {command} returned {process.ExitCode}: {detail}");
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            diagnostics.Write("NPCAP", $"service {command} failed: {ex.Message}");
            return false;
        }
    }
}
