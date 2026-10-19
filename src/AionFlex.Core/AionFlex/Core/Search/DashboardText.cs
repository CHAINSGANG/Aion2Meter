using System;
using AionFlex.Core.Alerts;

namespace AionFlex.Core.Search;

public static class DashboardText
{
	public static string SectionTitle(string section)
	{
		return section switch
		{
			"sessions" => "Fights", 
			"pvp" => "PvP fights", 
			"training" => "Training", 
			"events" => "Timers", 
			"checklist" => "Checklist", 
			"mechanics" => "Mechanics", 
			"pets" => "Pets", 
			"roster" => "Roster", 
			"logs" => "Logs", 
			"settings" => "Settings", 
			"license" => "Account", 
			_ => "", 
		};
	}

	public static string GroupTitle(string group)
	{
		string text = AlertPages.ForGroup(group)?.Title;
		if (text == null)
		{
			text = group switch
			{
				"overlay" => "Overlay", 
				"alerts" => "Alerts & sounds", 
				"windows" => "On-screen windows", 
				"general" => "General", 
				"fights" => "Fights & sessions", 
				"hotkeys" => "Hotkeys", 
				"data" => "Data & privacy", 
				"about" => "About", 
				_ => "", 
			};
		}
		return text;
	}

	public static string GroupLede(string group)
	{
		string text = AlertPages.ForGroup(group)?.Lede;
		if (text == null)
		{
			text = group switch
			{
				"overlay" => "How the DPS overlay looks over the game.", 
				"alerts" => "Warnings on the overlay before events, bosses and mechanics, and their sounds.", 
				"windows" => "Every window over the game in one place: show, hide, lock and put back each one.", 
				"general" => "Language and the region of your game server.", 
				"fights" => "What the meter keeps after a fight, and PvP.", 
				"hotkeys" => "Not while AION 2 has focus (Windows blocks them): they work alt-tabbed. Change or turn off each one.", 
				"data" => "What the app sends to aionflex.gg.", 
				"about" => "The version you are running.", 
				_ => "", 
			};
		}
		return text;
	}

	public static string GreetingFor(int hour)
	{
		switch (hour)
		{
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
			return "Good morning";
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
			return "Good afternoon";
		default:
			return "Good evening";
		}
	}

	public static string CountdownText(DateTime start, DateTime end, DateTime now)
	{
		if (start <= now)
		{
			if (!(end > now))
			{
				return "";
			}
			return "ON NOW";
		}
		TimeSpan timeSpan = start - now;
		if (!(timeSpan.TotalDays >= 1.0))
		{
			if (!(timeSpan.TotalHours >= 1.0))
			{
				return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
			}
			return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
		}
		return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
	}
}
