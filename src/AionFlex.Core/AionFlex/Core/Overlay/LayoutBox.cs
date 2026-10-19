using System;

namespace AionFlex.Core.Overlay;

public readonly record struct LayoutBox(double X, double Y, double W, double H)
{
	public double Right => X + W;

	public double Bottom => Y + H;

	public double IntersectionArea(LayoutBox other)
	{
		double num = Math.Min(Right, other.Right) - Math.Max(X, other.X);
		double num2 = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
		if (!(num <= 0.0) && !(num2 <= 0.0))
		{
			return num * num2;
		}
		return 0.0;
	}
}
