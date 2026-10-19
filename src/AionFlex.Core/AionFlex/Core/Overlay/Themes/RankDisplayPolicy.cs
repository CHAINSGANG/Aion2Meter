using System;
using System.Globalization;

namespace AionFlex.Core.Overlay.Themes;

public static class RankDisplayPolicy
{
	public static string Padded(int rank)
	{
		return Math.Max(0, rank).ToString("00", CultureInfo.InvariantCulture);
	}
}
