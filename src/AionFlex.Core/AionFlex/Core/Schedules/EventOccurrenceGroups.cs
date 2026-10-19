using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Schedules;

public static class EventOccurrenceGroups
{
	public static IReadOnlyList<EventOccurrence> Merge(IEnumerable<EventOccurrence> occurrences)
	{
		return (from o in occurrences
			group o by (Type: o.Type, TimeUntilStart: o.TimeUntilStart) into g
			orderby g.Key.TimeUntilStart
			select MergeGroup(g.ToList())).ToList();
	}

	private static EventOccurrence MergeGroup(IReadOnlyList<EventOccurrence> items)
	{
		if (items.Count == 1)
		{
			return items[0];
		}
		EventOccurrence eventOccurrence = items[0];
		EventText eventText = JoinNames(items);
		return new EventOccurrence
		{
			EventId = string.Join("+", items.Select((EventOccurrence o) => o.EventId)),
			Name = eventText.En,
			Names = eventText,
			Type = eventOccurrence.Type,
			NpcId = ((items.Select((EventOccurrence o) => o.NpcId).Distinct().Count() == 1) ? eventOccurrence.NpcId : ((int?)null)),
			LocalStart = eventOccurrence.LocalStart,
			LocalEnd = items.Max((EventOccurrence o) => o.LocalEnd),
			IsActive = items.Any((EventOccurrence o) => o.IsActive),
			IsNext = items.Any((EventOccurrence o) => o.IsNext),
			IsPast = items.All((EventOccurrence o) => o.IsPast),
			TimeUntilStart = eventOccurrence.TimeUntilStart,
			Locations = JoinLocations(items),
			Location = JoinLocations(items)?.En
		};
	}

	public static string ServerScheduleText(IReadOnlyList<GameEvent> events)
	{
		IEnumerable<string> values = from d in events.SelectMany((GameEvent e) => e.DaysOfWeek ?? Enum.GetValues<DayOfWeek>()).Distinct()
			orderby (int)(d + 6) % 7
			select CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(d);
		IEnumerable<string> values2 = from t in events.SelectMany((GameEvent e) => e.ServerTimes).Distinct()
			orderby t
			select t.ToString("HH:mm", CultureInfo.InvariantCulture);
		return string.Join(" / ", values) + " · " + string.Join(", ", values2) + " server time";
	}

	public static EventText JoinNames(IReadOnlyList<EventOccurrence> items)
	{
		return new EventText(Join(items, (EventOccurrence o) => o.Names.En), Join(items, (EventOccurrence o) => o.Names.Ko), Join(items, (EventOccurrence o) => o.Names.Zh));
	}

	private static EventText? JoinLocations(IReadOnlyList<EventOccurrence> items)
	{
		if (items.All((EventOccurrence o) => (object)o.Locations == null))
		{
			return null;
		}
		return new EventText(Distinct((EventText t) => t.En), Distinct((EventText t) => t.Ko), Distinct((EventText t) => t.Zh));
		string Distinct(Func<EventText, string> pick)
		{
			return string.Join(" · ", (from o in items
				where (object)o.Locations != null
				select pick(o.Locations)).Distinct());
		}
	}

	private static string Join(IReadOnlyList<EventOccurrence> items, Func<EventOccurrence, string> pick)
	{
		return string.Join(", ", from g in items.GroupBy(pick)
			select (g.Count() > 1) ? $"{g.Key} ×{g.Count()}" : g.Key);
	}
}
