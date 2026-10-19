using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public static class FloatingWindowPlacement
{
	public enum Spot
	{
		Saved,
		Right,
		Left,
		Below,
		Above,
		Centered
	}

	public const double GapDip = 8.0;

	public const double TitleStripDip = 24.0;

	public const double MinGrabWidthDip = 48.0;

	public static bool IsTitleStripOnAWorkArea(LayoutBox window, IReadOnlyList<LayoutBox> workAreas, double titleStripPx, double minGrabWidthPx)
	{
		if (workAreas == null || workAreas.Count == 0)
		{
			return false;
		}
		if (!IsFinite(window) || window.W <= 0.0 || window.H <= 0.0)
		{
			return false;
		}
		LayoutBox layoutBox = window with
		{
			H = Math.Min(window.H, Math.Max(1.0, titleStripPx))
		};
		double num = Math.Min(layoutBox.W, Math.Max(1.0, minGrabWidthPx));
		double num2 = layoutBox.H / 2.0;
		foreach (LayoutBox workArea in workAreas)
		{
			double num3 = Math.Min(layoutBox.Right, workArea.Right) - Math.Max(layoutBox.X, workArea.X);
			double num4 = Math.Min(layoutBox.Bottom, workArea.Bottom) - Math.Max(layoutBox.Y, workArea.Y);
			if (num3 >= num && num4 >= num2)
			{
				return true;
			}
		}
		return false;
	}

	public static (LayoutBox Box, Spot Spot) PlaceNextToOverlay(LayoutBox overlay, LayoutBox work, double width, double height, double gapPx)
	{
		double num = Math.Max(0.0, gapPx);
		(LayoutBox, Spot, bool)[] array = new (LayoutBox, Spot, bool)[4]
		{
			(new LayoutBox(overlay.Right + num, overlay.Y, width, height), Spot.Right, true),
			(new LayoutBox(overlay.X - num - width, overlay.Y, width, height), Spot.Left, true),
			(new LayoutBox(overlay.X, overlay.Bottom + num, width, height), Spot.Below, false),
			(new LayoutBox(overlay.X, overlay.Y - num - height, width, height), Spot.Above, false)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(LayoutBox, Spot, bool) tuple = array[i];
			LayoutBox item = tuple.Item1;
			Spot item2 = tuple.Item2;
			LayoutBox layoutBox = (tuple.Item3 ? SlideY(item, work) : SlideX(item, work));
			if (FitsInside(layoutBox, work) && layoutBox.IntersectionArea(overlay) <= 0.0)
			{
				return (Box: layoutBox, Spot: item2);
			}
		}
		return (Box: CenterIn(work, width, height), Spot: Spot.Centered);
	}

	public static LayoutBox CenterIn(LayoutBox work, double width, double height)
	{
		return BreakdownLayoutPolicy.ClampInto(new LayoutBox(work.X + (work.W - width) / 2.0, work.Y + (work.H - height) / 2.0, width, height), work);
	}

	public static (LayoutBox Box, Spot Spot) Resolve(LayoutBox? saved, double width, double height, LayoutBox overlay, LayoutBox overlayWork, IReadOnlyList<LayoutBox> allWorkAreas, double scale)
	{
		double num = ((double.IsFinite(scale) && scale > 0.0) ? scale : 1.0);
		if (saved.HasValue)
		{
			LayoutBox valueOrDefault = saved.GetValueOrDefault();
			if (IsTitleStripOnAWorkArea(valueOrDefault, allWorkAreas, 24.0 * num, 48.0 * num))
			{
				return (Box: valueOrDefault, Spot: Spot.Saved);
			}
		}
		return PlaceNextToOverlay(overlay, overlayWork, width, height, 8.0 * num);
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

	private static LayoutBox SlideX(LayoutBox box, LayoutBox work)
	{
		double num = box.X;
		if (num + box.W > work.Right)
		{
			num = work.Right - box.W;
		}
		if (num < work.X)
		{
			num = work.X;
		}
		return box with
		{
			X = num
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

	private static bool IsFinite(LayoutBox b)
	{
		if (double.IsFinite(b.X) && double.IsFinite(b.Y) && double.IsFinite(b.W))
		{
			return double.IsFinite(b.H);
		}
		return false;
	}
}
