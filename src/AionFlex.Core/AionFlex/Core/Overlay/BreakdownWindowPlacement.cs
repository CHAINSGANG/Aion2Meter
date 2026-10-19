using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public static class BreakdownWindowPlacement
{
	public enum Spot
	{
		Saved,
		BesideRight,
		BesideLeft,
		Cascade,
		NextToOverlay,
		Centered
	}

	public const double DefaultWidthDip = 820.0;

	public const double DefaultHeightDip = 900.0;

	public const double MinWidthDip = 480.0;

	public const double MinHeightDip = 600.0;

	public const double CompactBelowDip = 680.0;

	public static (double Width, double Height) SavedSize(double? width, double? height)
	{
		return (Width: Side(width, 820.0, 480.0), Height: Side(height, 900.0, 600.0));
	}

	public static bool IsCompact(double widthDip)
	{
		if (double.IsFinite(widthDip))
		{
			return widthDip < 680.0;
		}
		return false;
	}

	public static (LayoutBox Box, Spot Spot) Resolve(LayoutBox? saved, double width, double height, LayoutBox? beside, LayoutBox overlay, LayoutBox overlayWork, IReadOnlyList<LayoutBox> allWorkAreas, double scale)
	{
		double num = ((double.IsFinite(scale) && scale > 0.0) ? scale : 1.0);
		IReadOnlyList<LayoutBox> readOnlyList2;
		if (allWorkAreas == null || allWorkAreas.Count <= 0)
		{
			IReadOnlyList<LayoutBox> readOnlyList = new LayoutBox[1] { overlayWork };
			readOnlyList2 = readOnlyList;
		}
		else
		{
			readOnlyList2 = allWorkAreas;
		}
		IReadOnlyList<LayoutBox> readOnlyList3 = readOnlyList2;
		double num2 = 8.0 * num;
		if (beside.HasValue)
		{
			LayoutBox valueOrDefault = beside.GetValueOrDefault();
			LayoutBox work = MostOverlapping(valueOrDefault, readOnlyList3) ?? overlayWork;
			(double W, double H) tuple = Fit(width, height, work, num);
			double item = tuple.W;
			double item2 = tuple.H;
			LayoutBox layoutBox = SlideY(new LayoutBox(valueOrDefault.Right + num2, valueOrDefault.Y, item, item2), work);
			if (FitsInside(layoutBox, work) && !BreakdownLayoutPolicy.Overlaps(layoutBox, valueOrDefault) && !BreakdownLayoutPolicy.Overlaps(layoutBox, overlay))
			{
				return (Box: layoutBox, Spot: Spot.BesideRight);
			}
			LayoutBox layoutBox2 = SlideY(new LayoutBox(valueOrDefault.X - num2 - item, valueOrDefault.Y, item, item2), work);
			if (FitsInside(layoutBox2, work) && !BreakdownLayoutPolicy.Overlaps(layoutBox2, valueOrDefault) && !BreakdownLayoutPolicy.Overlaps(layoutBox2, overlay))
			{
				return (Box: layoutBox2, Spot: Spot.BesideLeft);
			}
			double num3 = 44.0 * num;
			LayoutBox layoutBox3 = BreakdownLayoutPolicy.ClampInto(new LayoutBox(valueOrDefault.X + num3, valueOrDefault.Y + num3, item, item2), work);
			if (!BreakdownLayoutPolicy.Overlaps(layoutBox3, overlay))
			{
				return (Box: layoutBox3, Spot: Spot.Cascade);
			}
			LayoutBox layoutBox4 = BreakdownLayoutPolicy.ClampInto(new LayoutBox(valueOrDefault.X - num3, valueOrDefault.Y + num3, item, item2), work);
			return (Box: BreakdownLayoutPolicy.Overlaps(layoutBox4, overlay) ? layoutBox3 : layoutBox4, Spot: Spot.Cascade);
		}
		if (saved.HasValue)
		{
			LayoutBox valueOrDefault2 = saved.GetValueOrDefault();
			if (FloatingWindowPlacement.IsTitleStripOnAWorkArea(valueOrDefault2, readOnlyList3, 24.0 * num, 48.0 * num))
			{
				return (Box: valueOrDefault2, Spot: Spot.Saved);
			}
		}
		var (width2, height2) = Fit(width, height, overlayWork, num);
		var (item3, spot) = FloatingWindowPlacement.PlaceNextToOverlay(overlay, overlayWork, width2, height2, num2);
		return (Box: item3, Spot: (spot == FloatingWindowPlacement.Spot.Centered) ? Spot.Centered : Spot.NextToOverlay);
	}

	private static (double W, double H) Fit(double width, double height, LayoutBox work, double scale)
	{
		double num = ((double.IsFinite(width) && width > 0.0) ? width : (820.0 * scale));
		double num2 = ((double.IsFinite(height) && height > 0.0) ? height : (900.0 * scale));
		if (work.W > 0.0 && num > work.W)
		{
			num = Math.Max(480.0 * scale, work.W);
		}
		if (work.H > 0.0 && num2 > work.H)
		{
			num2 = Math.Max(600.0 * scale, work.H);
		}
		return (W: num, H: num2);
	}

	private static double Side(double? value, double fallback, double min)
	{
		if (value.HasValue)
		{
			double valueOrDefault = value.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault) && valueOrDefault >= min)
			{
				return valueOrDefault;
			}
		}
		return fallback;
	}

	private static LayoutBox? MostOverlapping(LayoutBox box, IReadOnlyList<LayoutBox> areas)
	{
		LayoutBox? result = null;
		double num = 0.0;
		foreach (LayoutBox area in areas)
		{
			double num2 = box.IntersectionArea(area);
			if (num2 > num)
			{
				num = num2;
				result = area;
			}
		}
		return result;
	}

	private static LayoutBox SlideY(LayoutBox box, LayoutBox work)
	{
		double num = box.Y;
		if (num + box.H > work.Bottom)
		{
			num = work.Bottom - box.H;
		}
		if (num < work.Y)
		{
			num = work.Y;
		}
		return box with
		{
			Y = num
		};
	}

	private static bool FitsInside(LayoutBox box, LayoutBox work)
	{
		if (box.X >= work.X && box.Y >= work.Y && box.Right <= work.Right)
		{
			return box.Bottom <= work.Bottom;
		}
		return false;
	}
}
