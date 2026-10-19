using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Overlay;

public static class OverlayAutoHidePolicy
{
	public static readonly IReadOnlyList<int> DelayChoicesSeconds = new int[4] { 10, 30, 60, 120 };

	public const int DefaultDelaySeconds = 30;

	public static int NormalizeDelay(int seconds)
	{
		if (!DelayChoicesSeconds.Contains(seconds))
		{
			return 30;
		}
		return seconds;
	}

	public static bool ShouldHide(bool enabled, long nowMs, long lastActivityMs, int delaySeconds, bool blocked)
	{
		if (enabled && !blocked)
		{
			return nowMs - lastActivityMs >= (long)NormalizeDelay(delaySeconds) * 1000L;
		}
		return false;
	}

	public static bool IsClickThrough(bool locked, bool hidden)
	{
		return locked | hidden;
	}

	public static bool ShowsActivity(long prevBattleMs, long prevTotalDamage, long battleMs, long totalDamage)
	{
		if (battleMs <= prevBattleMs)
		{
			return totalDamage > prevTotalDamage;
		}
		return true;
	}
}
