using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Mechanics;

public static class MechanicsSheetGeometry
{
	public readonly record struct Chevron(double X, double Y, double AngleDegrees);

	public static IReadOnlyList<Chevron> PlaceChevrons(IReadOnlyList<SheetPoint> path, double step, double endMargin = 14.0)
	{
		List<Chevron> list = new List<Chevron>();
		if (path.Count < 2 || step <= 0.0)
		{
			return list;
		}
		double num = step * 0.5;
		for (int i = 1; i < path.Count; i++)
		{
			SheetPoint sheetPoint = path[i - 1];
			SheetPoint sheetPoint2 = path[i];
			double num2 = sheetPoint2.X - sheetPoint.X;
			double num3 = sheetPoint2.Y - sheetPoint.Y;
			double num4 = Math.Sqrt(num2 * num2 + num3 * num3);
			if (!(num4 <= 0.0))
			{
				double angleDegrees = Math.Atan2(num3, num2) * 180.0 / Math.PI;
				double num5;
				for (num5 = num; num5 < num4 - endMargin; num5 += step)
				{
					list.Add(new Chevron(sheetPoint.X + num2 * num5 / num4, sheetPoint.Y + num3 * num5 / num4, angleDegrees));
				}
				num = num5 - num4;
			}
		}
		return list;
	}

	public static (double Scale, double OffsetX, double OffsetY) FitCanvas(double canvasWidth, double canvasHeight, double actualWidth, double actualHeight)
	{
		if (canvasWidth <= 0.0 || canvasHeight <= 0.0 || actualWidth <= 0.0 || actualHeight <= 0.0)
		{
			return (Scale: 0.0, OffsetX: 0.0, OffsetY: 0.0);
		}
		double num = Math.Min(actualWidth / canvasWidth, actualHeight / canvasHeight);
		double item = (actualWidth - canvasWidth * num) / 2.0;
		double item2 = (actualHeight - canvasHeight * num) / 2.0;
		return (Scale: num, OffsetX: item, OffsetY: item2);
	}
}
