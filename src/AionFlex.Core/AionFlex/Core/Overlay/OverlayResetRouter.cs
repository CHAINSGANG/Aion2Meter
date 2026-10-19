using System;

namespace AionFlex.Core.Overlay;

public static class OverlayResetRouter
{
	public static void Route(OverlayMode mode, Action resetDps, Action resetPvp)
	{
		ArgumentNullException.ThrowIfNull(resetDps, "resetDps");
		ArgumentNullException.ThrowIfNull(resetPvp, "resetPvp");
		if (mode == OverlayMode.Pvp)
		{
			resetPvp();
		}
		else
		{
			resetDps();
		}
	}
}
