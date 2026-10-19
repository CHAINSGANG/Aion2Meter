using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Schedules;

public sealed class ServerClockAttribution
{
	public static readonly TimeSpan StableFor = TimeSpan.FromSeconds(30.0);

	private (TimeSpan Offset, GameRegion? Region)? _reading;

	private long _liveSinceMs;

	private bool _taken;

	public (GameRegion Region, TimeSpan Offset)? Live { get; private set; }

	public long LiveSinceMs => _liveSinceMs;

	public void OnReading(TimeSpan offset, GameRegion? namedRegion, long nowMs)
	{
		if (!namedRegion.HasValue)
		{
			(TimeSpan, GameRegion?)? reading = _reading;
			if (reading.HasValue)
			{
				(TimeSpan, GameRegion?) valueOrDefault = reading.GetValueOrDefault();
				if (valueOrDefault.Item1 == offset)
				{
					namedRegion = valueOrDefault.Item2;
				}
			}
		}
		_reading = (offset, namedRegion);
		if (namedRegion.HasValue)
		{
			GameRegion valueOrDefault2 = namedRegion.GetValueOrDefault();
			SetLive(valueOrDefault2, offset, nowMs);
		}
	}

	public void OnServerConfirmed(GameRegion region, long nowMs)
	{
		(TimeSpan, GameRegion?)? reading = _reading;
		if (!reading.HasValue)
		{
			return;
		}
		(TimeSpan, GameRegion?) valueOrDefault = reading.GetValueOrDefault();
		GameRegion? item = valueOrDefault.Item2;
		if (item.HasValue)
		{
			GameRegion valueOrDefault2 = item.GetValueOrDefault();
			if (valueOrDefault2 != region)
			{
				return;
			}
		}
		SetLive(region, valueOrDefault.Item1, nowMs);
	}

	public bool TryTakeStable(long nowMs, out (GameRegion Region, TimeSpan Offset) pair)
	{
		pair = default;
		(GameRegion, TimeSpan)? live = Live;
		if (live.HasValue)
		{
			(GameRegion, TimeSpan) valueOrDefault = live.GetValueOrDefault();
			if (!_taken && nowMs - _liveSinceMs >= (long)StableFor.TotalMilliseconds)
			{
				_taken = true;
				pair = valueOrDefault;
				return true;
			}
		}
		return false;
	}

	private void SetLive(GameRegion region, TimeSpan offset, long nowMs)
	{
		(GameRegion, TimeSpan)? live = Live;
		if (live.HasValue)
		{
			(GameRegion, TimeSpan) valueOrDefault = live.GetValueOrDefault();
			if (valueOrDefault.Item1 == region && valueOrDefault.Item2 == offset)
			{
				return;
			}
		}
		Live = (region, offset);
		_liveSinceMs = nowMs;
		_taken = false;
	}
}
