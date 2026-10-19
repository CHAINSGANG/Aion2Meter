using System;

namespace AionFlex.Core.Combat;

public static class EnrageTimerPolicy
{
	public const long RecentWindowMs = 30000L;

	public const long MinProjectionPullMs = 5000L;

	public const double TightMarginFraction = 0.1;

	public static bool IsTrialDifficulty(string? difficulty)
	{
		if (!(difficulty == "Ordeal"))
		{
			return difficulty?.StartsWith("Ascension Trial", StringComparison.Ordinal) ?? false;
		}
		return true;
	}

	public static EnrageTimerState Evaluate(in EnrageTimerInput input)
	{
		if (input.BossDead)
		{
			return EnrageTimerState.Hidden;
		}
		if (input.EnrageObservedAtMs.HasValue)
		{
			return EnrageTimerState.Enraged;
		}
		if (!input.IsTrial)
		{
			int? berserkSeconds = input.BerserkSeconds;
			if (berserkSeconds.HasValue && berserkSeconds.GetValueOrDefault() > 0)
			{
				long? pullFirstHitMs = input.PullFirstHitMs;
				if (pullFirstHitMs.HasValue)
				{
					long valueOrDefault = pullFirstHitMs.GetValueOrDefault();
					long num = (long)input.BerserkSeconds.Value * 1000L;
					long num2 = input.NowMs - valueOrDefault;
					long num3 = num - num2;
					if (num3 <= 0)
					{
						return EnrageTimerState.Enraged;
					}
					long? projectedKillMs = ProjectKillMs(in input, num2);
					int num4;
					if (projectedKillMs.HasValue)
					{
						long valueOrDefault2 = projectedKillMs.GetValueOrDefault();
						num4 = (int)VerdictOf(valueOrDefault2, num3, input.BerserkSeconds.Value);
					}
					else
					{
						num4 = 0;
					}
					EnrageVerdict verdict = (EnrageVerdict)num4;
					return new EnrageTimerState(EnrageTimerKind.Countdown, num3, projectedKillMs, verdict);
				}
			}
		}
		return EnrageTimerState.Hidden;
	}

	public static EnrageVerdict VerdictOf(long projectedKillMs, long enrageLeftMs, int berserkSeconds)
	{
		if (projectedKillMs > enrageLeftMs)
		{
			return EnrageVerdict.TooSlow;
		}
		long num = (long)((double)((long)berserkSeconds * 1000L) * 0.1);
		if (projectedKillMs <= enrageLeftMs - num)
		{
			return EnrageVerdict.OnTrack;
		}
		return EnrageVerdict.Tight;
	}

	private static long? ProjectKillMs(in EnrageTimerInput input, long elapsedMs)
	{
		if (elapsedMs < 5000 || input.BossCurrentHp <= 0)
		{
			return null;
		}
		long num;
		long num2;
		if (elapsedMs < 30000)
		{
			long pullDamage = input.PullDamage;
			num = pullDamage;
			num2 = elapsedMs;
		}
		else
		{
			long pullDamage = input.RecentDamage;
			num = pullDamage;
			num2 = 30000L;
		}
		if (num <= 0)
		{
			return null;
		}
		return (long)Math.Ceiling((double)input.BossCurrentHp * (double)num2 / (double)num);
	}
}
