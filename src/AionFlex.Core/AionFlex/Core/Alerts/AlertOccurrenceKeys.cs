using System;
using AionFlex.Core.Combat;
using AionFlex.Core.Schedules;

namespace AionFlex.Core.Alerts;

public static class AlertOccurrenceKeys
{
	public static string Rift(bool open, DateTime localStart)
	{
		return $"{(open ? "open" : "upcoming")}:{localStart.Ticks}";
	}

	public static string Scheduled(EventAlertPhase phase, DateTime localStart)
	{
		return $"{phase}:{localStart.Ticks}";
	}

	public static string? Enrage(EnrageTimerKind kind, int bossEntityId, long? pullFirstHitMs)
	{
		if (pullFirstHitMs.HasValue)
		{
			long valueOrDefault = pullFirstHitMs.GetValueOrDefault();
			if (bossEntityId > 0)
			{
				return kind switch
				{
					EnrageTimerKind.Countdown => $"countdown:{bossEntityId}:{valueOrDefault}", 
					EnrageTimerKind.Enraged => $"enraged:{bossEntityId}:{valueOrDefault}", 
					_ => null, 
				};
			}
		}
		return null;
	}

	public static string? Mechanic(int bossEntityId, int epoch, string mechanicId, bool broken)
	{
		if (bossEntityId > 0 && !string.IsNullOrEmpty(mechanicId))
		{
			return $"{(broken ? "broken" : "fire")}:{bossEntityId}:{epoch}:{mechanicId}";
		}
		return null;
	}
}
