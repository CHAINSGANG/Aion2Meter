using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AionFlex.Core.Training;

public static class TrainingSessionNames
{
	public static string DefaultName(DateTime localStart)
	{
		return localStart.ToString("ddd d MMM", CultureInfo.InvariantCulture) + " · " + localStart.ToString("HH:mm", CultureInfo.InvariantCulture);
	}

	public static bool IsAutoName(string? name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			return AutoNameRegex().IsMatch(name.Trim());
		}
		return true;
	}

	public static string CustomName(string? name)
	{
		if (!IsAutoName(name))
		{
			return name.Trim();
		}
		return "";
	}

	public static string TimeOfDay(DateTime localStart, bool withSeconds)
	{
		return localStart.ToString(withSeconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);
	}

	public static string Label(string? storedName, DateTime localStart, bool withSeconds)
	{
		string text = CustomName(storedName);
		if (text.Length > 0)
		{
			return text;
		}
		return localStart.ToString("ddd d MMM", CultureInfo.InvariantCulture) + " · " + TimeOfDay(localStart, withSeconds);
	}

	public static string DayHeading(DateOnly day, DateOnly today)
	{
		if (day == today)
		{
			return "Today";
		}
		if (day == today.AddDays(-1))
		{
			return "Yesterday";
		}
		string text = ((day.Year == today.Year) ? "dddd d MMMM" : "dddd d MMMM yyyy");
		return day.ToString(text, CultureInfo.InvariantCulture);
	}

	private static readonly Regex AutoName = new("^(Session \\d{1,4}|Session \\d{1,2}:\\d{2}|(Mon|Tue|Wed|Thu|Fri|Sat|Sun) \\d{1,2} [A-Z][a-z]{2} · \\d{2}:\\d{2})$", RegexOptions.Compiled);

	private static Regex AutoNameRegex() => AutoName;
}
