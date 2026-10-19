using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public static class HeaderOverflowPolicy
{
	public const double MinBossBandWidth = 96.0;

	public static double BarBudget(double stripWidth, double timerWidth, double portraitWidth, bool classicHeader)
	{
		if (!classicHeader)
		{
			return stripWidth;
		}
		return stripWidth - timerWidth - portraitWidth - 96.0;
	}

	public static IReadOnlyList<int> Plan(double available, IReadOnlyList<HeaderItem> items, double overflowToggleWidth)
	{
		if (items == null || items.Count == 0)
		{
			return Array.Empty<int>();
		}
		double num = 0.0;
		for (int i = 0; i < items.Count; i++)
		{
			num += items[i].Width;
		}
		if (num <= available)
		{
			return Array.Empty<int>();
		}
		List<int> list = new List<int>();
		for (int j = 0; j < items.Count; j++)
		{
			if (items[j].CanOverflow)
			{
				list.Add(j);
			}
		}
		list.Sort((int a, int b) =>
		{
			int num3 = items[a].Priority.CompareTo(items[b].Priority);
			return (num3 == 0) ? b.CompareTo(a) : num3;
		});
		List<int> list2 = new List<int>();
		double num2 = num + overflowToggleWidth;
		foreach (int item in list)
		{
			if (num2 <= available)
			{
				break;
			}
			list2.Add(item);
			num2 -= items[item].Width;
		}
		return list2;
	}
}
