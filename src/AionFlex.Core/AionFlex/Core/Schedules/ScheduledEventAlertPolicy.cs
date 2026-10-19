using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Schedules;

public static class ScheduledEventAlertPolicy
{
	public const int MinMinutesBefore = 1;

	public const int MaxMinutesBefore = 30;

	public static readonly TimeSpan StartedWindow = TimeSpan.FromMinutes(3.0);

	public static ScheduledEventAlert? Evaluate(GameEventType kind, IEnumerable<EventOccurrence> occurrences, EventAlertPreferences preferences)
	{
		if (!preferences.Enabled)
		{
			return null;
		}
		TimeSpan lead = TimeSpan.FromMinutes(Math.Clamp(preferences.MinutesBefore, 1, 30));
		List<(DateTime, TimeSpan, IReadOnlyList<EventOccurrence>)> source = (from o in occurrences
			where o.Type == kind
			group o by o.LocalStart into g
			select ((DateTime Start, TimeSpan Until, IReadOnlyList<EventOccurrence> Items))(Start: g.Key, Until: g.First().TimeUntilStart, Items: g.ToList())).ToList();
		(DateTime, TimeSpan, IReadOnlyList<EventOccurrence>) tuple = source.Where(((DateTime Start, TimeSpan Until, IReadOnlyList<EventOccurrence> Items) g) => g.Until <= TimeSpan.Zero && -g.Until < StartedWindow).OrderByDescending(((DateTime Start, TimeSpan Until, IReadOnlyList<EventOccurrence> Items) g) => g.Until).FirstOrDefault();
		if (tuple.Item3 != null)
		{
			return new ScheduledEventAlert(kind, EventAlertPhase.Started, tuple.Item1, tuple.Item2, tuple.Item3);
		}
		(DateTime, TimeSpan, IReadOnlyList<EventOccurrence>) tuple2 = source.Where(((DateTime Start, TimeSpan Until, IReadOnlyList<EventOccurrence> Items) g) => g.Until > TimeSpan.Zero && g.Until <= lead).OrderBy(((DateTime Start, TimeSpan Until, IReadOnlyList<EventOccurrence> Items) g) => g.Until).FirstOrDefault();
		if (tuple2.Item3 != null)
		{
			return new ScheduledEventAlert(kind, EventAlertPhase.Upcoming, tuple2.Item1, tuple2.Item2, tuple2.Item3);
		}
		return null;
	}
}
