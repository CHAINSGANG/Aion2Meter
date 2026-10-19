namespace AionFlex.Core.Checklist;

public sealed record ChecklistProgress(int DailyDone, int DailyTotal, int WeeklyDone, int WeeklyTotal)
{
	public bool AllDailyDone
	{
		get
		{
			if (DailyTotal > 0)
			{
				return DailyDone >= DailyTotal;
			}
			return false;
		}
	}

	public bool AllWeeklyDone
	{
		get
		{
			if (WeeklyTotal > 0)
			{
				return WeeklyDone >= WeeklyTotal;
			}
			return false;
		}
	}
}
