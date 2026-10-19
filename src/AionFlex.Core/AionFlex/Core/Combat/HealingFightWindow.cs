namespace AionFlex.Core.Combat;

public readonly record struct HealingFightWindow(long StartMs, long EndMs)
{
	public static readonly HealingFightWindow None = new HealingFightWindow(long.MaxValue, long.MinValue);

	public static readonly HealingFightWindow Unbounded = new HealingFightWindow(long.MinValue, long.MaxValue);

	public bool Contains(long timestampMs)
	{
		if (timestampMs >= StartMs)
		{
			return timestampMs <= EndMs;
		}
		return false;
	}

	public static HealingFightWindow For(bool bossOnlyMode, int targetId, long targetFirstDamageMs, long finishedFightEndMs)
	{
		if (!bossOnlyMode)
		{
			return Unbounded;
		}
		if (targetId <= 0 || targetFirstDamageMs <= 0)
		{
			return None;
		}
		return new HealingFightWindow(targetFirstDamageMs, finishedFightEndMs);
	}
}
