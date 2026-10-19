using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Overlay.Themes;

public static class ThemeRowColorPolicy
{
	public static uint Resolve(OverlayRowColorMode mode, JobClass job, uint singleArgb, IReadOnlyDictionary<JobClass, uint>? classOverrides, uint fallbackArgb)
	{
		return OverlayRowColorPolicy.ResolveRowColor((mode == OverlayRowColorMode.Gold) ? OverlayRowColorMode.Class : mode, job, singleArgb, classOverrides) ?? fallbackArgb;
	}
}
