using System;

namespace AionFlex.Core.Combat;

public static class FinishedFightPolicy
{
	public static long EndMs(bool bossOnlyMode, bool reachedZeroHp, long zeroHpMs, long lastDamageOnBossMs)
	{
		if (!bossOnlyMode || !reachedZeroHp || zeroHpMs <= 0)
		{
			return long.MaxValue;
		}
		return Math.Max(zeroHpMs, lastDamageOnBossMs);
	}
}
