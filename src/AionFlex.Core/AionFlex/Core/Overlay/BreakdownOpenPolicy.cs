namespace AionFlex.Core.Overlay;

public static class BreakdownOpenPolicy
{
	public const int MaxOpenPopups = 2;

	public static bool ShouldCloseOther(bool clickedWillOpen, bool otherIsPinned)
	{
		if (clickedWillOpen)
		{
			return !otherIsPinned;
		}
		return false;
	}
}
