using System;
using Microsoft.Extensions.Logging;

namespace Aion2Meter.Services;

internal sealed class DiagnosticLogger<T>(DiagnosticLog diagnostics, LogLevel minimumLevel) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= minimumLevel && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        string message = formatter(state, exception);
        if (exception is not null)
            message += Environment.NewLine + exception;

        diagnostics.Write(typeof(T).Name.ToUpperInvariant(), $"{logLevel}: {message}");
    }
}
