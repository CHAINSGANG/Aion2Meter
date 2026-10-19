using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Alerts;

public static class AlertPages
{
	public const int ExampleMinutes = 5;

	public const string GroupPrefix = "alerts/";

	public static IReadOnlyList<AlertPage> All { get; } = new AlertPage[8]
	{
		new AlertPage("rift", AlertSoundKind.Rift, "Spacetime Rift", "A countdown before each Spacetime Rift opens, then how long it stays open.", "\ue787", "#CC3d8bff", "#CC6366f1", "Rift in", "RIFT OPEN", "09:59", IsPro: false),
		new AlertPage("siege", AlertSoundKind.Siege, "Artifact Siege", "A countdown before every Artifact Siege, on your server's clock.", "\ue7c1", "#CCE23B4E", "#CC9F1D35", "Artifact Siege in", "Artifact Siege started", "NOW", IsPro: true),
		new AlertPage("abyssBoss", AlertSoundKind.AbyssBoss, "Abyss bosses", "A countdown before the Reshanta bosses that spawn at fixed times: Guardian Lord Nahma, the Executors, Watcher Kaira.", "\uecad", "#CC8B5CF6", "#CC5B21B6", "Guardian Lord Nahma in", "Guardian Lord Nahma spawned", "NOW", IsPro: true),
		new AlertPage("shugoFesta", AlertSoundKind.ShugoFesta, "Shugo Festival", "A countdown before the hourly Shugo Festival (Global), then that its entry is open for 3 minutes.", "\ue7fc", "#CC14B8A6", "#CC0F766E", "Shugo Festival in", "Shugo Festival open (3 min to enter)", "NOW", IsPro: true),
		new AlertPage("invasion", AlertSoundKind.Invasion, "Dimensional Invasion", "A countdown before the hourly Dimensional Invasion (Global; every hour at :30, server time), then that its entry is open for 3 minutes.", "\ue945", "#CCF97316", "#CC9A3412", "Dimensional Invasion in", "Dimensional Invasion open (3 min to enter)", "NOW", IsPro: true),
		new AlertPage("fieldBoss", AlertSoundKind.FieldBoss, "Field bosses", "A countdown before the field bosses you tick respawn, from the world map's Field Boss list (Global).", "\ue823", "#CCCBA135", "#CC8A6A1E", "Blooming Korin in", "Blooming Korin is up", "NOW", IsPro: true),
		new AlertPage("bossEnrage", AlertSoundKind.BossEnrage, "Boss enrage", "The time left before the boss enrages and the kill time at your party's DPS; ENRAGED when it does.", "\ue916", "#CC2F9E6E", "#CC1C6B49", "Enrage 4:00 · kill ~2:58", "ENRAGED", "", IsPro: true, "\ue7ba", "#E6B0102A", "#E66E0A1C"),
		new AlertPage("mechanics", AlertSoundKind.Mechanics, "Boss mechanics", "A warning when the boss's stagger check starts, a confirmation when it breaks, and when Ultimate Berk jumps to the pillars.", "\ue7ba", "#E6E23B4E", "#E67A1226", "~50% HP — STAGGER CHECK; time your big stagger skills or the party wipes", "Stagger broken ✓", "", IsPro: true, "\ue73e", "#CC2F9E74", "#CC1B6B4E")
	};

	public static string Group(string key)
	{
		return "alerts/" + key;
	}

	public static AlertPage? ForGroup(string? group)
	{
		if (group == null || !group.StartsWith("alerts/", StringComparison.Ordinal))
		{
			return null;
		}
		return All.FirstOrDefault((AlertPage p) =>
		{
			string key = p.Key;
			string text = group;
			int length = "alerts/".Length;
			return key == text.Substring(length, text.Length - length);
		});
	}

	public static AlertPage For(AlertSoundKind kind)
	{
		return All.First((AlertPage p) => p.Kind == kind);
	}

	public static string ExampleCountdown(AlertPage page, int? minutesBefore)
	{
		AlertSoundKind kind = page.Kind;
		if ((kind != AlertSoundKind.BossEnrage && kind != AlertSoundKind.Mechanics) || 1 == 0)
		{
			return $"{Math.Max(1, minutesBefore ?? 5):D2}:00";
		}
		return "";
	}
}
