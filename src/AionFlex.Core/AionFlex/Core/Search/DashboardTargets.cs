using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Alerts;

namespace AionFlex.Core.Search;

public static class DashboardTargets
{
	public const string Start = "start";

	public const string Sessions = "sessions";

	public const string Pvp = "pvp";

	public const string Training = "training";

	public const string Events = "events";

	public const string Checklist = "checklist";

	public const string Mechanics = "mechanics";

	public const string Pets = "pets";

	public const string Roster = "roster";

	public const string Logs = "logs";

	public const string Settings = "settings";

	public const string License = "license";

	public const string SettingsPrefix = "settings/";

	public static readonly IReadOnlyList<string> SettingsGroups = new string[8] { "overlay", "alerts", "windows", "general", "fights", "hotkeys", "data", "about" };

	public const string FightPrefix = "fight:";

	public static string SettingsGroup(string group)
	{
		return "settings/" + group;
	}

	public static string AlertPage(string key)
	{
		return "settings/" + AlertPages.Group(key);
	}

	public static string Fight(string sessionFilePath)
	{
		return "fight:" + sessionFilePath;
	}

	public static string? FightPath(string? target)
	{
		if (target == null || !target.StartsWith("fight:", StringComparison.Ordinal) || target.Length <= "fight:".Length)
		{
			return null;
		}
		int length = "fight:".Length;
		return target.Substring(length, target.Length - length);
	}

	public static (string Section, string Group) Parse(string? target)
	{
		if (string.IsNullOrWhiteSpace(target))
		{
			return (Section: "start", Group: "");
		}
		if (target.StartsWith("fight:", StringComparison.Ordinal))
		{
			return (Section: "sessions", Group: "");
		}
		if (target.StartsWith("settings/", StringComparison.Ordinal))
		{
			int length = "settings/".Length;
			string text = target.Substring(length, target.Length - length);
			if ((object)AlertPages.ForGroup(text) != null)
			{
				return (Section: "settings", Group: text);
			}
			if (!SettingsGroups.Contains(text))
			{
				return (Section: "settings", Group: "");
			}
			return (Section: "settings", Group: text);
		}
		return (Section: target, Group: "");
	}
}
