using System;
using System.Collections.Generic;

namespace AionFlex.Core.Parsing;

public sealed class InventoryChangeDedup
{
	public const long WindowMs = 5000L;

	private const int MaxRemembered = 64;

	private readonly Queue<(string Key, long AtMs)> _recent = new Queue<(string, long)>();

	public bool ShouldEmit(ReadOnlySpan<byte> record, long nowMs)
	{
		string text = Convert.ToHexString(record);
		while (_recent.Count > 0 && (nowMs - _recent.Peek().AtMs >= 5000 || nowMs < _recent.Peek().AtMs))
		{
			_recent.Dequeue();
		}
		foreach (var item in _recent)
		{
			if (item.Key == text)
			{
				return false;
			}
		}
		_recent.Enqueue((text, nowMs));
		while (_recent.Count > 64)
		{
			_recent.Dequeue();
		}
		return true;
	}
}
