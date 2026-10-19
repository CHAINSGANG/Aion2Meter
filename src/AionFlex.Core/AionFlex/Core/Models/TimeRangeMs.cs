namespace AionFlex.Core.Models;

public readonly record struct TimeRangeMs(long StartMs, long EndMs)
{
	public long DurationMs => EndMs - StartMs;
}
