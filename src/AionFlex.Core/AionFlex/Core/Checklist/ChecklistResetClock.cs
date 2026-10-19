using System;

namespace AionFlex.Core.Checklist;

public sealed record ChecklistResetClock(TimeSpan DailyAtUtc, DayOfWeek WeeklyDay, bool IsGameReset)
{
	public static ChecklistResetClock Default { get; } = new ChecklistResetClock(TimeSpan.Zero, DayOfWeek.Monday, IsGameReset: false);
}
