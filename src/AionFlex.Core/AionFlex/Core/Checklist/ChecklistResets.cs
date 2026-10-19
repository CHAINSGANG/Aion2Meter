using System;
using System.Collections.Generic;
using AionFlex.Core.ContentsTickets;
using AionFlex.Core.Models;

namespace AionFlex.Core.Checklist;

public static class ChecklistResets
{
	public static readonly IReadOnlyDictionary<string, TimeOnly> GameDaily = new Dictionary<string, TimeOnly> { ["Global"] = new TimeOnly(16, 0) };

	public const DayOfWeek GameWeeklyDay = DayOfWeek.Wednesday;

	public static ChecklistResetClock ForRegion(GameRegion? datacenter)
	{
		string text;
		if (datacenter.HasValue)
		{
			GameRegion valueOrDefault = datacenter.GetValueOrDefault();
			text = (((uint)valueOrDefault > 1u) ? "Global" : null);
		}
		else
		{
			text = null;
		}
		string text2 = text;
		if (text2 != null)
		{
			TimeSpan? timeSpan = ContentsTicketClock.ServerOffset(datacenter);
			if (timeSpan.HasValue)
			{
				TimeSpan valueOrDefault2 = timeSpan.GetValueOrDefault();
				if (GameDaily.TryGetValue(text2, out var value))
				{
					TimeSpan timeSpan2 = value.ToTimeSpan() - valueOrDefault2;
					DayOfWeek dayOfWeek = DayOfWeek.Wednesday;
					if (timeSpan2 < TimeSpan.Zero)
					{
						timeSpan2 += TimeSpan.FromDays(1.0);
						dayOfWeek = (DayOfWeek)((int)(dayOfWeek + 6) % 7);
					}
					return new ChecklistResetClock(timeSpan2, dayOfWeek, IsGameReset: true);
				}
			}
		}
		return ChecklistResetClock.Default;
	}

	public static DateOnly DailyPeriod(DateTime utcNow)
	{
		return DailyPeriod(utcNow, ChecklistResetClock.Default);
	}

	public static DateOnly DailyPeriod(DateTime utcNow, ChecklistResetClock clock)
	{
		return DateOnly.FromDateTime(utcNow - clock.DailyAtUtc);
	}

	public static DateOnly WeeklyPeriod(DateTime utcNow)
	{
		return WeeklyPeriod(utcNow, ChecklistResetClock.Default);
	}

	public static DateOnly WeeklyPeriod(DateTime utcNow, ChecklistResetClock clock)
	{
		DateTime dateTime = utcNow - clock.DailyAtUtc;
		int num = (dateTime.DayOfWeek - clock.WeeklyDay + 7) % 7;
		return DateOnly.FromDateTime(dateTime.AddDays(-num));
	}

	public static DateTime NextDaily(DateTime utcNow)
	{
		return NextDaily(utcNow, ChecklistResetClock.Default);
	}

	public static DateTime NextDaily(DateTime utcNow, ChecklistResetClock clock)
	{
		return DailyPeriod(utcNow, clock).AddDays(1).ToDateTime(TimeOnly.MinValue) + clock.DailyAtUtc;
	}

	public static DateTime NextWeekly(DateTime utcNow)
	{
		return NextWeekly(utcNow, ChecklistResetClock.Default);
	}

	public static DateTime NextWeekly(DateTime utcNow, ChecklistResetClock clock)
	{
		return WeeklyPeriod(utcNow, clock).AddDays(7).ToDateTime(TimeOnly.MinValue) + clock.DailyAtUtc;
	}

	public static string Countdown(DateTime utcNow, DateTime target)
	{
		TimeSpan timeSpan = target - utcNow;
		if (timeSpan <= TimeSpan.Zero)
		{
			return "now";
		}
		if (!(timeSpan.TotalDays >= 1.0))
		{
			if (!(timeSpan.TotalHours >= 1.0))
			{
				return $"{Math.Max(1, (int)Math.Ceiling(timeSpan.TotalMinutes))}m";
			}
			return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes:D2}m";
		}
		return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
	}
}
