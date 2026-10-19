using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay.Themes;

public static class NameLineCombatPower
{
	public readonly record struct Row(bool HasCombatPower, double Need, double Room);

	public const double MinNameRoomEm = 5.0;

	public const double WholeNameSlack = 1.0;

	public static bool OnNameLine(bool entitled, OverlayDensity density, bool subLineShown)
	{
		if (entitled && density != OverlayDensity.Micro)
		{
			return !subLineShown;
		}
		return false;
	}

	public static double NameReserve(double nameFontSize, double nameWidth)
	{
		double num = ((double.IsFinite(nameFontSize) && nameFontSize > 0.0) ? (5.0 * nameFontSize) : double.NaN);
		double num2 = ((double.IsFinite(nameWidth) && nameWidth > 0.0) ? (nameWidth + 1.0) : double.NaN);
		if (double.IsNaN(num))
		{
			if (!double.IsNaN(num2))
			{
				return num2;
			}
			return 0.0;
		}
		if (!double.IsNaN(num2))
		{
			return Math.Min(num, num2);
		}
		return num;
	}

	public static bool Shows(IEnumerable<Row> rows)
	{
		bool result = false;
		foreach (Row row in rows)
		{
			if (row.HasCombatPower)
			{
				if (!double.IsFinite(row.Need) || !double.IsFinite(row.Room) || row.Room + 0.5 < row.Need)
				{
					return false;
				}
				result = true;
			}
		}
		return result;
	}
}
