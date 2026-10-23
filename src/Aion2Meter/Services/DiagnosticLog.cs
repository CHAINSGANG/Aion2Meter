using System;
using System.IO;
using System.Text;

namespace Aion2Meter.Services;

public sealed class DiagnosticLog : IDisposable
{
    private const long DefaultMaximumFileBytes = 16 * 1024 * 1024;
    private const int MaximumFilesPerRun = 8;
    private readonly object _sync = new();
    private StreamWriter _writer;
    private readonly long _maximumFileBytes;
    private readonly string _fileStem;
    private int _fileNumber = 1;
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
        _fileStem = Path.Combine(directory, $"hp-debug-{stamp}");
        FilePath = _fileStem + ".log";
        _writer = OpenWriter(FilePath);
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
                    if (_fileNumber >= MaximumFilesPerRun)
                    {
                        _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [LOG] file size limit reached; further diagnostics suppressed");
                        _limitReached = true;
                        return;
                    }

                    int nextFileNumber = _fileNumber + 1;
                    string nextPath = $"{_fileStem}-part{nextFileNumber:00}.log";
                    StreamWriter nextWriter = OpenWriter(nextPath);
                    _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [LOG] continuing in {Path.GetFileName(nextPath)}");
                    _writer.Dispose();
                    _writer = nextWriter;
                    _fileNumber = nextFileNumber;
                    _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [START] diagnostics continuation part {_fileNumber}");
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

    private static StreamWriter OpenWriter(string path) => new(
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
    {
        AutoFlush = true
    };

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
