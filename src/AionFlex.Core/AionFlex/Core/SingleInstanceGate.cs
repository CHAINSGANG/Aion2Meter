using System;
using System.Runtime.Versioning;
using System.Threading;

namespace AionFlex.Core;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceGate : IDisposable
{
	public enum Outcome
	{
		Acquired,
		AlreadyRunning,
		StillClosing
	}

	public static readonly TimeSpan DefaultClosingWait = TimeSpan.FromSeconds(60.0);

	private static readonly TimeSpan NoEventRetry = TimeSpan.FromMilliseconds(250.0);

	private readonly string _closingEventName;

	private Mutex? _mutex;

	private EventWaitHandle? _closingSignal;

	private EventWaitHandle? _showSignal;

	private RegisteredWaitHandle? _showWait;

	private SingleInstanceGate(Mutex mutex, string closingEventName)
	{
		_mutex = mutex;
		_closingEventName = closingEventName;
	}

	public static Outcome TryAcquire(string mutexName, string closingEventName, TimeSpan closingWait, out SingleInstanceGate? gate)
	{
		gate = null;
		Mutex mutex = new Mutex(initiallyOwned: false, mutexName);
		try
		{
			if (TryTake(mutex, TimeSpan.Zero))
			{
				gate = new SingleInstanceGate(mutex, closingEventName);
				return Outcome.Acquired;
			}
			if (!EventWaitHandle.TryOpenExisting(closingEventName, out EventWaitHandle result))
			{
				if (TryTake(mutex, NoEventRetry))
				{
					gate = new SingleInstanceGate(mutex, closingEventName);
					return Outcome.Acquired;
				}
				mutex.Dispose();
				return Outcome.AlreadyRunning;
			}
			using (result)
			{
				if (TryTake(mutex, closingWait))
				{
					gate = new SingleInstanceGate(mutex, closingEventName);
					return Outcome.Acquired;
				}
			}
			mutex.Dispose();
			return Outcome.StillClosing;
		}
		catch
		{
			mutex.Dispose();
			throw;
		}
	}

	private static bool TryTake(Mutex mutex, TimeSpan wait)
	{
		try
		{
			return mutex.WaitOne(wait);
		}
		catch (AbandonedMutexException)
		{
			return true;
		}
	}

	public void SignalClosing()
	{
		if (_closingSignal != null)
		{
			return;
		}
		try
		{
			_closingSignal = new EventWaitHandle(initialState: true, EventResetMode.ManualReset, _closingEventName);
		}
		catch
		{
		}
	}

	public bool ListenForShow(string showEventName, Action onShow)
	{
		if (_showSignal != null)
		{
			return true;
		}
		try
		{
			_showSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, showEventName);
			_showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (object? _, bool _) =>
			{
				try
				{
					onShow();
				}
				catch
				{
				}
			}, null, -1, executeOnlyOnce: false);
			return true;
		}
		catch
		{
			_showSignal?.Dispose();
			_showSignal = null;
			return false;
		}
	}

	public static bool TrySignalShow(string showEventName)
	{
		try
		{
			if (!EventWaitHandle.TryOpenExisting(showEventName, out EventWaitHandle result))
			{
				return false;
			}
			using (result)
			{
				return result.Set();
			}
		}
		catch
		{
			return false;
		}
	}

	public void Dispose()
	{
		_showWait?.Unregister(null);
		_showWait = null;
		_showSignal?.Dispose();
		_showSignal = null;
		Mutex mutex = Interlocked.Exchange(ref _mutex, null);
		if (mutex != null)
		{
			try
			{
				mutex.ReleaseMutex();
			}
			catch (ApplicationException)
			{
			}
			mutex.Dispose();
		}
		_closingSignal?.Dispose();
		_closingSignal = null;
	}
}
