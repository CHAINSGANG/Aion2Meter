using System;

namespace AionFlex.Core.Combat;

public readonly record struct ActiveProcItem(int SkillCode, ProcBarSection Section, int Order, long StartMs, long EndMs, long DurationMs, int Stacks)
{
	public long RemainingMs(long nowMs)
	{
		return Math.Max(0L, EndMs - nowMs);
	}

	public double ElapsedFraction(long nowMs)
	{
		if (DurationMs > 0)
		{
			return Math.Clamp((double)(nowMs - StartMs) / (double)DurationMs, 0.0, 1.0);
		}
		return 1.0;
	}
}
