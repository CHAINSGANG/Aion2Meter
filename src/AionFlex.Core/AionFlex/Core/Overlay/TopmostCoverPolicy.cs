using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public static class TopmostCoverPolicy
{
	public const int MaxWindowsAbove = 512;

	public static bool Covers(in WindowAboveUs window, uint ownProcessId, ScreenRect ownBounds)
	{
		if (window.IsVisible && !window.IsCloaked && window.IsTopmost && window.ProcessId != ownProcessId)
		{
			return window.Bounds.OverlapsWithPositiveArea(ownBounds);
		}
		return false;
	}

	public static TopmostCoverDecision Walk(IZOrderReader zOrder, nint ownHwnd, uint ownProcessId, ScreenRect ownBounds, List<WindowAboveUs> windowsAbove)
	{
		windowsAbove.Clear();
		nint hwnd = ownHwnd;
		for (nint num = zOrder.WindowAbove(ownHwnd); num != 0; num = zOrder.WindowAbove(num))
		{
			if (windowsAbove.Count >= 512)
			{
				return new TopmostCoverDecision(ShouldRaise: true, -1, TopmostCoverReason.WalkIncomplete);
			}
			WindowAboveUs window = zOrder.Describe(num);
			windowsAbove.Add(window);
			if (Covers(in window, ownProcessId, ownBounds))
			{
				return new TopmostCoverDecision(ShouldRaise: true, windowsAbove.Count - 1, TopmostCoverReason.CoveredByForeignTopmost);
			}
			hwnd = num;
		}
		bool walkComplete = zOrder.Exists(hwnd);
		return Decide(windowsAbove, walkComplete, ownProcessId, ownBounds);
	}

	public static TopmostCoverDecision Decide(IReadOnlyList<WindowAboveUs> windowsAbove, bool walkComplete, uint ownProcessId, ScreenRect ownBounds)
	{
		for (int i = 0; i < windowsAbove.Count; i++)
		{
			if (Covers(windowsAbove[i], ownProcessId, ownBounds))
			{
				return new TopmostCoverDecision(ShouldRaise: true, i, TopmostCoverReason.CoveredByForeignTopmost);
			}
		}
		if (!walkComplete)
		{
			return new TopmostCoverDecision(ShouldRaise: true, -1, TopmostCoverReason.WalkIncomplete);
		}
		return new TopmostCoverDecision(ShouldRaise: false, -1, TopmostCoverReason.NotCovered);
	}
}
