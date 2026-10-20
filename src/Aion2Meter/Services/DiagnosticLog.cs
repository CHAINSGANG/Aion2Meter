using System;
using System.IO;
using System.Text;

namespace Aion2Meter.Services;

public sealed class DiagnosticLog : IDisposable
{
    private const long DefaultMaximumFileBytes = 16 * 1024 * 1024;
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private readonly long _maximumFileBytes;
    private bool _disposed;
    private bool _faulted;
    private bool _limitReached;

    public DiagnosticLog(string directory) : this(directory, DefaultMaximumFileBytes)
    { }

    internal DiagnosticLog(string directory, long maximumFileBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileBytes);
        _maximumFileBytes = maximumFileBytes;
        Directory.CreateDirectory(directory);
        string stamp = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
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
            if (_disposed || _faulted || _limitReached)
            {
                return;
            }

            try
            {
                if (_writer.BaseStream.Position >= _maximumFileBytes)
                {
                    _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [LOG] file size limit reached; further diagnostics suppressed");
                    _limitReached = true;
                    return;
                }
                _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{category}] {message}");
            }
            catch (IOException)
            {
                _faulted = true;
            }
            catch (UnauthorizedAccessException)
            {
                _faulted = true;
            }
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

            _disposed = true;
            try
            {
                if (!_faulted && !_limitReached)
                    _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [STOP] Aion2Meter diagnostics stopped");
            }
            catch (IOException)
            {
                // Logging failure must never interrupt application shutdown.
            }
            catch (UnauthorizedAccessException)
            {
                // Logging failure must never interrupt application shutdown.
            }
            try
            {
                _writer.Dispose();
            }
            catch (IOException)
            {
                // Logging failure must never interrupt application shutdown.
            }
            catch (UnauthorizedAccessException)
            {
                // Logging failure must never interrupt application shutdown.
            }
        }
    }
}
