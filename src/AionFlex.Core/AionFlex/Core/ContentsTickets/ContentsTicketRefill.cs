using System;

namespace AionFlex.Core.ContentsTickets;

public sealed record ContentsTicketRefill(int Count, TimeOnly At, DayOfWeek? Day = null, int EverySeconds = 0)
{
	public bool IsDaily
	{
		get
		{
			if (!Day.HasValue)
			{
				return EverySeconds == 0;
			}
			return false;
		}
	}

	public bool IsWeekly => Day.HasValue;

	public bool IsEvery => EverySeconds > 0;
}
