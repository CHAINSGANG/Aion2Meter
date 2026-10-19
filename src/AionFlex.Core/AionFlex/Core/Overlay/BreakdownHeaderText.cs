using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public static class BreakdownHeaderText
{
	public static string FightLine(string? boss, string? zone, string? difficulty, string? battleTime)
	{
		List<string> list = new List<string>(3);
		if (Has(boss))
		{
			list.Add(boss.Trim());
		}
		if (Has(zone))
		{
			list.Add(Has(difficulty) ? (zone.Trim() + " (" + difficulty.Trim() + ")") : zone.Trim());
		}
		if (Has(battleTime))
		{
			list.Add("fight " + battleTime.Trim());
		}
		return string.Join(" · ", list);
	}

	public static string ShortFightLine(string? boss, string? battleTime)
	{
		List<string> list = new List<string>(2);
		if (Has(boss))
		{
			list.Add(boss.Trim());
		}
		if (Has(battleTime))
		{
			list.Add(battleTime.Trim());
		}
		return string.Join(" · ", list);
	}

	private static bool Has(string? s)
	{
		return !string.IsNullOrWhiteSpace(s);
	}
}
