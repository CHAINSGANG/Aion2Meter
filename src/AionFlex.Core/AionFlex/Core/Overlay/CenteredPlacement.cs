using System;

namespace AionFlex.Core.Overlay;

public static class CenteredPlacement
{
	public static (double Left, double Top) Center(double workLeft, double workTop, double workWidth, double workHeight, double width, double height)
	{
		double item = workLeft + Math.Max(0.0, (workWidth - width) / 2.0);
		double item2 = workTop + Math.Max(0.0, (workHeight - height) / 2.0);
		return (Left: item, Top: item2);
	}
}
