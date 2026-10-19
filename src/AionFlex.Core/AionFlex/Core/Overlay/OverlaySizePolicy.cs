using System;

namespace AionFlex.Core.Overlay;

public static class OverlaySizePolicy
{
	public static double? ResolveRestoreWidth(double storedWidth, double minWidth)
	{
		if (!(storedWidth >= minWidth))
		{
			return null;
		}
		return storedWidth;
	}

	public static double ComputeMaxHeight(double workAreaBottomDip, double windowTopDip, double minHeight)
	{
		if (double.IsNaN(workAreaBottomDip) || double.IsInfinity(workAreaBottomDip) || double.IsNaN(windowTopDip))
		{
			return double.PositiveInfinity;
		}
		double val = workAreaBottomDip - windowTopDip;
		return Math.Max(minHeight, val);
	}
}
