using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Search;

namespace AionFlex.Core.Schedules;

public static class TimersBoard
{
	public static readonly TimeSpan SiegeWarmUp = TimeSpan.FromMinutes(10.0);

	public static readonly TimeSpan WithinHourSpan = TimeSpan.FromHours(1.0);

	public static readonly TimeSpan WeekSpan = TimeSpan.FromDays(7.0);

	public const double MinSweepDegrees = 2.5;

	public static string Title(TimerBucket bucket)
	{
		return bucket switch
		{
			TimerBucket.OnNow => "On now", 
			TimerBucket.WithinHour => "Within the hour", 
			TimerBucket.LaterToday => "Later today", 
			_ => "This week", 
		};
	}

	public static TimerBucket? BucketOf(TimerItem item, DateTime now)
	{
		if (item.UpNow)
		{
			return TimerBucket.OnNow;
		}
		if (item.Start <= now)
		{
			if (!(item.End > now))
			{
				return null;
			}
			return TimerBucket.OnNow;
		}
		TimeSpan timeSpan = item.Start - now;
		if (timeSpan <= WithinHourSpan)
		{
			return TimerBucket.WithinHour;
		}
		if (item.Start.Date == now.Date)
		{
			return TimerBucket.LaterToday;
		}
		if (!(timeSpan <= WeekSpan))
		{
			return null;
		}
		return TimerBucket.ThisWeek;
	}

	public static IReadOnlyList<TimerGroup> Group(IEnumerable<TimerItem> items, DateTime now, TimerFilter filter = TimerFilter.All)
	{
		List<(TimerItem, TimerBucket?)> source = (from i in items
			where Matches(i, filter)
			select (Item: i, Bucket: BucketOf(i, now))).Where(((TimerItem Item, TimerBucket? Bucket) x) =>
		{
			TimerBucket? item = x.Bucket;
			return item.HasValue;
		}).ToList();
		List<TimerGroup> list = new List<TimerGroup>();
		TimerBucket[] values = Enum.GetValues<TimerBucket>();
		foreach (TimerBucket bucket in values)
		{
			List<TimerItem> list2 = (from i in source.Where(((TimerItem Item, TimerBucket? Bucket) x) => x.Bucket == bucket).Select(((TimerItem Item, TimerBucket? Bucket) x) => x.Item)
				orderby i.UpNow descending, i.Start
				select i).ThenBy((TimerItem i) => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
			if (bucket == TimerBucket.ThisWeek)
			{
				HashSet<string> earlier = source.Where(((TimerItem Item, TimerBucket? Bucket) x) => x.Bucket != TimerBucket.ThisWeek).Select(((TimerItem Item, TimerBucket? Bucket) x) => x.Item.Key).ToHashSet();
				list2 = (from i in list2
					where !earlier.Contains(i.Key)
					group i by i.Key into g
					select g.First()).ToList();
			}
			if (list2.Count > 0)
			{
				list.Add(new TimerGroup(bucket, Title(bucket), list2));
			}
		}
		return list;
	}

	public static bool Matches(TimerItem item, TimerFilter filter)
	{
		switch (filter)
		{
		case TimerFilter.Abyss:
		{
			TimerKind kind = item.Kind;
			return (uint)(kind - 1) <= 1u;
		}
		case TimerFilter.FieldBosses:
			return item.Kind == TimerKind.FieldBoss;
		case TimerFilter.Rifts:
			return item.Kind == TimerKind.Rift;
		case TimerFilter.Shugo:
			return item.Kind == TimerKind.Shugo;
		case TimerFilter.Invasion:
			return item.Kind == TimerKind.Invasion;
		default:
			return true;
		}
	}

	public static string Countdown(TimerItem item, DateTime now)
	{
		if (item.UpNow)
		{
			return "UP";
		}
		if (item.Start <= now && item.End > now)
		{
			TimeSpan timeSpan = item.End - now;
			string text = $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
			switch (item.Kind)
			{
			case TimerKind.Siege:
				return "bosses in " + text;
			case TimerKind.Shugo:
			case TimerKind.Invasion:
				return "closes in " + text;
			default:
				return "ON NOW";
			}
		}
		return DashboardText.CountdownText(item.Start, item.End, now);
	}

	public static IReadOnlyList<DialArc> DialArcs(IEnumerable<TimerItem> items, DateTime now)
	{
		DateTime date = now.Date;
		DateTime dateTime = date.AddDays(1.0);
		List<DialArc> list = new List<DialArc>();
		foreach (TimerItem item in items)
		{
			if (item.UpNow)
			{
				continue;
			}
			DateTime dateTime2 = ((item.Start < date) ? date : item.Start);
			DateTime dateTime3 = ((item.End > dateTime) ? dateTime : item.End);
			if (!(dateTime2 >= dateTime) && !(dateTime3 <= date) && !(item.Start >= dateTime))
			{
				double num = (dateTime2 - date).TotalMinutes / 1440.0 * 360.0;
				double val = Math.Max(2.5, (dateTime3 - dateTime2).TotalMinutes / 1440.0 * 360.0);
				List<DialArc> list2 = list;
				double startDegrees = num;
				double sweepDegrees = Math.Min(val, 360.0 - num);
				TimerKind kind = item.Kind;
				bool ring;
				switch (item.Kind)
				{
				case TimerKind.Siege:
				case TimerKind.Shugo:
				case TimerKind.Invasion:
					ring = true;
					break;
				default:
					ring = false;
					break;
				}
				list2.Add(new DialArc(startDegrees, sweepDegrees, kind, ring ? 1 : 0));
			}
		}
		return list;
	}

	public static double NowDegrees(DateTime now)
	{
		return (now - now.Date).TotalMinutes / 1440.0 * 360.0;
	}

	public static string Accent(TimerKind kind)
	{
		return kind switch
		{
			TimerKind.Rift => "#FF7EB0FF", 
			TimerKind.Siege => "#FFF07A8B", 
			TimerKind.AbyssBoss => "#FFB9A0FF", 
			TimerKind.Shugo => "#FF5FD3C3", 
			TimerKind.Invasion => "#FFF59E5B", 
			_ => "#FFCBA135", 
		};
	}
}
