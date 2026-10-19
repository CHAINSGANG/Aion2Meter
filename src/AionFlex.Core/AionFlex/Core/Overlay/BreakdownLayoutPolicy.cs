namespace AionFlex.Core.Overlay;

public static class BreakdownLayoutPolicy
{
	public const double GapDip = 8.0;

	public const double CascadeDip = 44.0;

	public static bool Overlaps(LayoutBox a, LayoutBox b)
	{
		return a.IntersectionArea(b) > 0.0;
	}

	public static LayoutBox ClampInto(LayoutBox box, LayoutBox work)
	{
		double num = box.X;
		double num2 = box.Y;
		if (num + box.W > work.Right)
		{
			num = work.Right - box.W;
		}
		if (num < work.X)
		{
			num = work.X;
		}
		if (num2 + box.H > work.Bottom)
		{
			num2 = work.Bottom - box.H;
		}
		if (num2 < work.Y)
		{
			num2 = work.Y;
		}
		return box with
		{
			X = num,
			Y = num2
		};
	}
}
