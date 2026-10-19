using System;

namespace AionFlex.Core.FieldBosses;

public static class FieldBossTimerPolicy
{
	public static readonly TimeSpan KeepAfterRespawn = TimeSpan.FromHours(24.0);

	public static readonly TimeSpan KeepUnseen = TimeSpan.FromHours(24.0);

	public static FieldBossStatus StatusAt(FieldBossTimer? timer, long nowUtcMs)
	{
		if ((object)timer == null)
		{
			return new FieldBossStatus(FieldBossStatusKind.Unknown, null, null);
		}
		TimeSpan value = TimeSpan.FromMilliseconds(Math.Max(0L, nowUtcMs - timer.LastSeenUtcMs));
		if (timer.Alive)
		{
			return new FieldBossStatus(FieldBossStatusKind.Alive, null, value);
		}
		long num = timer.TimeUtcMs - nowUtcMs;
		if (num <= 0)
		{
			return new FieldBossStatus(FieldBossStatusKind.ProbablyUp, null, value);
		}
		return new FieldBossStatus(FieldBossStatusKind.RespawnsIn, TimeSpan.FromMilliseconds(num), value);
	}

	public static bool IsExpired(FieldBossTimer timer, long nowUtcMs)
	{
		if (nowUtcMs - timer.LastSeenUtcMs > (long)KeepUnseen.TotalMilliseconds)
		{
			return true;
		}
		if (!timer.Alive)
		{
			return nowUtcMs - timer.TimeUtcMs > (long)KeepAfterRespawn.TotalMilliseconds;
		}
		return false;
	}

	public static string Countdown(TimeSpan remaining)
	{
		if (remaining < TimeSpan.Zero)
		{
			remaining = TimeSpan.Zero;
		}
		long num = (long)Math.Ceiling(remaining.TotalSeconds);
		long num2 = num / 3600;
		long value = num / 60 % 60;
		long value2 = num % 60;
		if (num2 > 0)
		{
			return $"{num2}:{value:D2}:{value2:D2}";
		}
		return $"{value}:{value2:D2}";
	}

	public static string Age(TimeSpan since)
	{
		if (since < TimeSpan.FromMinutes(1.0))
		{
			return "just now";
		}
		if (!(since < TimeSpan.FromHours(1.0)))
		{
			return $"{(int)since.TotalHours}h ago";
		}
		return $"{(int)since.TotalMinutes}m ago";
	}
}
