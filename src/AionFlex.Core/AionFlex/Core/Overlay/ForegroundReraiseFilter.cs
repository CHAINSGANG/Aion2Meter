namespace AionFlex.Core.Overlay;

public static class ForegroundReraiseFilter
{
	public const uint EventSystemForeground = 3u;

	public static bool IsForeignForegroundChange(uint eventType, int idObject, int idChild, nint foregroundHwnd, nint ownHwnd, uint foregroundProcessId, uint ownProcessId)
	{
		if (eventType == 3 && ownHwnd != 0 && idObject == 0 && idChild == 0 && foregroundHwnd != ownHwnd)
		{
			return foregroundProcessId != ownProcessId;
		}
		return false;
	}
}
