using System;
using System.Threading;

namespace Aion2Meter.Services;

internal sealed class SingleInstanceGate : IDisposable
{
    private Mutex? _mutex;

    public SingleInstanceGate(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
        IsPrimary = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }

    public bool IsPrimary { get; }

    public void Dispose()
    {
        Mutex? mutex = _mutex;
        _mutex = null;
        if (mutex is null)
            return;

        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
