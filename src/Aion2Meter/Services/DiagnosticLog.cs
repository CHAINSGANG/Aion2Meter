using System;
using System.IO;
using System.Text;

namespace Aion2Meter.Services;

public sealed class DiagnosticLog : IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public DiagnosticLog(string directory)
    {
        Directory.CreateDirectory(directory);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        FilePath = Path.Combine(directory, $"hp-debug-{stamp}.log");
        var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
        Write("START", $"Aion2Meter diagnostics started; base={AppContext.BaseDirectory}");
    }

    public string FilePath { get; }

    public void Write(string category, string message)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{category}] {message}");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [STOP] Aion2Meter diagnostics stopped");
            _disposed = true;
            _writer.Dispose();
        }
    }
}
