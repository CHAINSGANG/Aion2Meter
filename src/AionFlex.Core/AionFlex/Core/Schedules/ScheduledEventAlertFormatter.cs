using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Schedules;

public static class ScheduledEventAlertFormatter
{
	public static EventAlertText Format(ScheduledEventAlert alert, Language language)
	{
		List<string> list = alert.Occurrences.Select((EventOccurrence o) => o.Names.For(language)).Distinct().ToList();
		string text = list.Count switch
		{
			0 => alert.Kind switch
			{
				GameEventType.Siege => "Siege", 
				GameEventType.ShugoFesta => "Shugo Festival", 
				GameEventType.Invasion => "Dimensional Invasion", 
				_ => "Boss", 
			}, 
			1 => list[0], 
			_ => $"{list[0]} +{list.Count - 1}", 
		};
		string text3;
		string countdown;
		if (alert.Phase == EventAlertPhase.Started)
		{
			string text2;
			switch (alert.Kind)
			{
			case GameEventType.Siege:
				text2 = text + " started";
				break;
			case GameEventType.ShugoFesta:
			case GameEventType.Invasion:
				text2 = $"{text} open ({EntryMinutes(alert)} min to enter)";
				break;
			default:
				text2 = text + " spawned";
				break;
			}
			text3 = text2;
			countdown = "NOW";
		}
		else
		{
			text3 = text + " in";
			countdown = Countdown(alert.TimeUntilStart);
		}
		IEnumerable<string> values = from o in alert.Occurrences
			group o by (Name: o.Names.For(language), Where: o.Locations?.For(language)) into g
			select ((g.Count() > 1) ? $"{g.Key.Name} ×{g.Count()}" : g.Key.Name) + (string.IsNullOrEmpty(g.Key.Where) ? "" : (" — " + g.Key.Where));
		string detail = string.Join("\n", values) + $"\nat {alert.LocalStart:HH:mm} (your time)";
		return new EventAlertText(text3, countdown, detail);
	}

	private static int EntryMinutes(ScheduledEventAlert alert)
	{
		EventOccurrence eventOccurrence = alert.Occurrences.FirstOrDefault();
		return Math.Max(1, (int)Math.Round(((eventOccurrence == null) ? TimeSpan.Zero : (eventOccurrence.LocalEnd - eventOccurrence.LocalStart)).TotalMinutes));
	}

	public static string Countdown(TimeSpan remaining)
	{
		if (remaining < TimeSpan.Zero)
		{
			remaining = TimeSpan.Zero;
		}
		if (remaining.TotalMinutes >= 1.0)
		{
			return $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
		}
		return $"{remaining.Seconds}s";
	}
}
