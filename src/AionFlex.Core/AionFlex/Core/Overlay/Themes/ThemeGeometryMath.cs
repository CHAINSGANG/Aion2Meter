using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay.Themes;

public static class ThemeGeometryMath
{
	public static double SkewAngleDeg(double slantPx, double heightPx)
	{
		if (!(heightPx <= 0.0))
		{
			return (0.0 - Math.Atan(slantPx / heightPx)) * 180.0 / Math.PI;
		}
		return 0.0;
	}

	public static (double X, double Y)[] Parallelogram(double width, double height, double slant)
	{
		return new (double, double)[4]
		{
			(slant, 0.0),
			(width, 0.0),
			(width - slant, height),
			(0.0, height)
		};
	}

	public static (double X, double Y)[] Octagon(double size)
	{
		double num = 0.3 * size;
		double num2 = 0.7 * size;
		return new (double, double)[8]
		{
			(num, 0.0),
			(num2, 0.0),
			(size, num),
			(size, num2),
			(num2, size),
			(num, size),
			(0.0, num2),
			(0.0, num)
		};
	}

	public static (double Width, double OffsetX) PrismTip(double fillWidth)
	{
		double num = Math.Max(0.0, fillWidth);
		double num2 = Math.Min(36.0, num);
		return (Width: num2, OffsetX: num - num2);
	}

	public static IReadOnlyList<(double X, double Width)> SegmentGaps(double width, int segments = 20, double gap = 2.0)
	{
		if (width <= 0.0 || segments <= 0)
		{
			return Array.Empty<(double, double)>();
		}
		(double, double)[] array = new (double, double)[segments];
		for (int i = 1; i <= segments; i++)
		{
			array[i - 1] = ((double)i * width / (double)segments - gap, gap);
		}
		return array;
	}

	public static (double Band, double FromX, double ToX) AetherSweep(double trackWidth)
	{
		double num = 0.38 * Math.Max(0.0, trackWidth);
		return (Band: num, FromX: -1.6 * num, ToX: 3.0 * num);
	}
}
