using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.ContentsTickets;

public static class ContentsTicketClock
{
	public static TimeSpan? ServerOffset(GameRegion? datacenter)
	{
		if (datacenter.HasValue && datacenter == GameRegion.EU)
		{
			return TimeSpan.FromHours(9.0);
		}
		return null;
	}

	public static DateTime Next(ContentsTicketRefill refill, TimeSpan serverOffset, DateTime nowUtc)
	{
		DateTime dateTime = nowUtc + serverOffset;
		DateTime dateTime3;
		if (refill.IsEvery)
		{
			DateTime dateTime2 = dateTime.Date + refill.At.ToTimeSpan();
			TimeSpan timeSpan = TimeSpan.FromSeconds(refill.EverySeconds);
			if (dateTime2 > dateTime)
			{
				dateTime2 -= TimeSpan.FromDays(1.0);
			}
			long num = (long)Math.Floor((double)(dateTime - dateTime2).Ticks / (double)timeSpan.Ticks) + 1;
			dateTime3 = dateTime2 + TimeSpan.FromTicks(timeSpan.Ticks * num);
		}
		else
		{
			dateTime3 = dateTime.Date + refill.At.ToTimeSpan();
			if (refill.IsWeekly)
			{
				int num2 = (refill.Day.Value - dateTime3.DayOfWeek + 7) % 7;
				dateTime3 = dateTime3.AddDays(num2);
				if (dateTime3 <= dateTime)
				{
					dateTime3 = dateTime3.AddDays(7.0);
				}
			}
			else if (dateTime3 <= dateTime)
			{
				dateTime3 = dateTime3.AddDays(1.0);
			}
		}
		return DateTime.SpecifyKind(dateTime3 - serverOffset, DateTimeKind.Utc);
	}

	public static (DateTime Utc, int Count)? NextOf(ContentsTicketInfo info, TimeSpan serverOffset, DateTime nowUtc)
	{
		(DateTime, int)? result = null;
		foreach (ContentsTicketRefill refill in info.Refills)
		{
			DateTime dateTime = Next(refill, serverOffset, nowUtc);
			if (!result.HasValue || dateTime < result.Value.Item1)
			{
				result = (dateTime, refill.Count);
			}
		}
		return result;
	}
}
