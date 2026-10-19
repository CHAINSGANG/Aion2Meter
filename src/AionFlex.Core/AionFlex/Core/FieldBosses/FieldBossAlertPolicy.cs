using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Localization;
using AionFlex.Core.Schedules;

namespace AionFlex.Core.FieldBosses;

public static class FieldBossAlertPolicy
{
	public const int MinMinutesBefore = 1;

	public const int MaxMinutesBefore = 60;

	public const int DefaultMinutesBefore = 5;

	public static readonly TimeSpan UpWindow = ScheduledEventAlertPolicy.StartedWindow;

	public static readonly TimeSpan MergeWindow = TimeSpan.FromMinutes(1.0);

	public static int ClampMinutes(int minutes)
	{
		return Math.Clamp(minutes, 1, 60);
	}

	public static FieldBossAlert? Evaluate(IEnumerable<FieldBossTimer> currentServerTimers, IFieldBossCatalog catalog, FieldBossAlertPreferences preferences, long nowUtcMs)
	{
		if (!preferences.Enabled || preferences.BossIds.Count == 0)
		{
			return null;
		}
		ISet<int> set = (preferences.BossIds as ISet<int>) ?? new HashSet<int>(preferences.BossIds);
		TimeSpan lead = TimeSpan.FromMinutes(ClampMinutes(preferences.MinutesBefore));
		List<(FieldBossInfo, long)> list = new List<(FieldBossInfo, long)>();
		foreach (FieldBossTimer currentServerTimer in currentServerTimers)
		{
			if (set.Contains(currentServerTimer.RowId))
			{
				FieldBossInfo fieldBossInfo = catalog.Find(currentServerTimer.RowId);
				if ((object)fieldBossInfo != null && fieldBossInfo.MapId == currentServerTimer.MapId)
				{
					list.Add((fieldBossInfo, currentServerTimer.TimeUtcMs));
				}
			}
		}
		if (preferences.WhenUp)
		{
			List<(FieldBossInfo, long)> list2 = list.Where(((FieldBossInfo Boss, long At) m) => m.At <= nowUtcMs && nowUtcMs - m.At < (long)UpWindow.TotalMilliseconds).OrderByDescending(((FieldBossInfo Boss, long At) m) => m.At).ThenBy(((FieldBossInfo Boss, long At) m) => m.Boss.RowId)
				.ToList();
			if (list2.Count > 0)
			{
				(FieldBossInfo Boss, long At) first = list2[0];
				List<(FieldBossInfo, long)> bosses = list2.Where(((FieldBossInfo Boss, long At) m) => first.At - m.At < (long)MergeWindow.TotalMilliseconds).Select(((FieldBossInfo Boss, long At) m) => (Boss: m.Boss, At: m.At)).ToList();
				return new FieldBossAlert(EventAlertPhase.Started, first.At, TimeSpan.FromMilliseconds(first.At - nowUtcMs), bosses);
			}
		}
		List<(FieldBossInfo, long)> list3 = list.Where(((FieldBossInfo Boss, long At) m) => m.At > nowUtcMs && m.At - nowUtcMs <= (long)lead.TotalMilliseconds).OrderBy(((FieldBossInfo Boss, long At) m) => m.At).ThenBy(((FieldBossInfo Boss, long At) m) => m.Boss.RowId)
			.ToList();
		if (list3.Count == 0)
		{
			return null;
		}
		(FieldBossInfo Boss, long At) soonest = list3[0];
		List<(FieldBossInfo, long)> bosses2 = list3.Where(((FieldBossInfo Boss, long At) m) => m.At - soonest.At < (long)MergeWindow.TotalMilliseconds).Select(((FieldBossInfo Boss, long At) m) => (Boss: m.Boss, At: m.At)).ToList();
		return new FieldBossAlert(EventAlertPhase.Upcoming, soonest.At, TimeSpan.FromMilliseconds(soonest.At - nowUtcMs), bosses2);
	}

	public static EventAlertText Format(FieldBossAlert alert, IFieldBossCatalog catalog, Language language)
	{
		string text = alert.Bosses[0].Boss.Name.Get(language);
		string text2 = ((alert.Bosses.Count > 1) ? $"{text} +{alert.Bosses.Count - 1}" : text);
		string text3;
		string countdown;
		if (alert.Phase == EventAlertPhase.Started)
		{
			text3 = ((alert.Bosses.Count > 1) ? (text2 + " are up") : (text2 + " is up"));
			countdown = "NOW";
		}
		else
		{
			text3 = text2 + " in";
			countdown = ScheduledEventAlertFormatter.Countdown(alert.TimeUntil);
		}
		IEnumerable<string> values = alert.Bosses.Select(((FieldBossInfo Boss, long AtUtcMs) b) =>
		{
			string text4 = catalog.FindMap(b.Boss.MapId)?.Name.Get(language);
			DateTimeOffset value = DateTimeOffset.FromUnixTimeMilliseconds(b.AtUtcMs).ToLocalTime();
			return $"{b.Boss.Name.Get(language)}{(string.IsNullOrEmpty(text4) ? "" : (" — " + text4))} at {value:HH:mm} (your time)";
		});
		return new EventAlertText(text3, countdown, string.Join("\n", values));
	}
}
