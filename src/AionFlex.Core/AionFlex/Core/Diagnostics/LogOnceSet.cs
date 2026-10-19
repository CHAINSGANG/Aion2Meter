using System;
using System.Collections.Generic;

namespace AionFlex.Core.Diagnostics;

public sealed class LogOnceSet<TKey> where TKey : notnull
{
	private readonly HashSet<TKey> _seen;

	private readonly Queue<TKey> _order = new Queue<TKey>();

	private readonly object _lock = new object();

	public int Capacity { get; }

	public int Count
	{
		get
		{
			lock (_lock)
			{
				return _seen.Count;
			}
		}
	}

	public LogOnceSet(int capacity, IEqualityComparer<TKey>? comparer = null)
	{
		if (capacity < 1)
		{
			throw new ArgumentOutOfRangeException("capacity", "capacity must be at least 1");
		}
		Capacity = capacity;
		_seen = new HashSet<TKey>(comparer);
	}

	public bool TryAdd(TKey key)
	{
		lock (_lock)
		{
			if (!_seen.Add(key))
			{
				return false;
			}
			_order.Enqueue(key);
			if (_order.Count > Capacity)
			{
				_seen.Remove(_order.Dequeue());
			}
			return true;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_seen.Clear();
			_order.Clear();
		}
	}
}
