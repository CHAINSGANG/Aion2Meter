using System;
using System.Collections.Generic;
using System.Globalization;

namespace AionFlex.Core.Diagnostics;

public sealed class LogRateGate
{
	private sealed class Entry
	{
		public long LastReportMs;

		public long LastSeenMs;

		public long Pending;
	}

	public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(60.0);

	public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(10.0);

	public const int DefaultMaxKeys = 256;

	private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

	private readonly object _lock = new object();

	private readonly long _windowMs;

	private readonly long _idleMs;

	private readonly int _maxKeys;

	private readonly Func<long> _clockMs;

	private readonly string _summarySuffixFormat;

	public TimeSpan Window { get; }

	public int TrackedKeyCount
	{
		get
		{
			lock (_lock)
			{
				return _entries.Count;
			}
		}
	}

	public LogRateGate(TimeSpan? window = null, int maxKeys = 256, TimeSpan? idleTimeout = null, Func<long>? clockMs = null)
	{
		Window = window ?? DefaultWindow;
		if (Window <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException("window", "window must be positive");
		}
		if (maxKeys < 1)
		{
			throw new ArgumentOutOfRangeException("maxKeys", "maxKeys must be at least 1");
		}
		TimeSpan timeSpan = idleTimeout ?? DefaultIdleTimeout;
		if (timeSpan <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException("idleTimeout", "idleTimeout must be positive");
		}
		_windowMs = (long)Window.TotalMilliseconds;
		_idleMs = (long)timeSpan.TotalMilliseconds;
		_maxKeys = maxKeys;
		_clockMs = clockMs ?? ((Func<long>)(() => Environment.TickCount64));
		_summarySuffixFormat = " — ×{0} since last report (" + Window.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s window)";
	}

	public bool ShouldLog(string key, out long repeats)
	{
		ArgumentNullException.ThrowIfNull(key, "key");
		lock (_lock)
		{
			long num = _clockMs();
			if (_entries.TryGetValue(key, out Entry value) && num - value.LastSeenMs > _idleMs)
			{
				_entries.Remove(key);
				value = null;
			}
			if (value == null)
			{
				if (_entries.Count >= _maxKeys)
				{
					EvictLocked(num);
				}
				_entries[key] = new Entry
				{
					LastReportMs = num,
					LastSeenMs = num
				};
				repeats = 0L;
				return true;
			}
			value.LastSeenMs = num;
			value.Pending++;
			if (num - value.LastReportMs >= _windowMs)
			{
				repeats = value.Pending;
				value.Pending = 0L;
				value.LastReportMs = num;
				return true;
			}
			repeats = 0L;
			return false;
		}
	}

	public string SummarySuffix(long repeats)
	{
		if (repeats > 0)
		{
			return string.Format(CultureInfo.InvariantCulture, _summarySuffixFormat, repeats);
		}
		return string.Empty;
	}

	private void EvictLocked(long now)
	{
		List<string> list = null;
		string key;
		Entry value;
		foreach (KeyValuePair<string, Entry> entry3 in _entries)
		{
			entry3.Deconstruct(out key, out value);
			string item = key;
			Entry entry = value;
			if (now - entry.LastSeenMs > _idleMs)
			{
				(list ?? (list = new List<string>())).Add(item);
			}
		}
		if (list != null)
		{
			foreach (string item2 in list)
			{
				_entries.Remove(item2);
			}
		}
		if (_entries.Count < _maxKeys)
		{
			return;
		}
		string text = null;
		long num = long.MaxValue;
		foreach (KeyValuePair<string, Entry> entry4 in _entries)
		{
			entry4.Deconstruct(out key, out value);
			string text2 = key;
			Entry entry2 = value;
			if (entry2.LastSeenMs < num)
			{
				num = entry2.LastSeenMs;
				text = text2;
			}
		}
		if (text != null)
		{
			_entries.Remove(text);
		}
	}
}
