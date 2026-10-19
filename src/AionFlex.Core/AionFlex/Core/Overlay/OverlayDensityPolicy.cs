namespace AionFlex.Core.Overlay;

public static class OverlayDensityPolicy
{
	public static OverlayDensityMetrics For(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayDensityMetrics(26.0, 1.0, "8,0,6,0", 17.0, 5.0, 12.0, 12.0, 10.0, 10.0, ShowClassIcon: true, ShowRankTitle: false, ShowTotalDamage: true, ShowSecondaryBadges: false, StackMetricsHorizontally: true), 
			OverlayDensity.Micro => new OverlayDensityMetrics(18.0, 1.0, "7,0,5,0", 0.0, 0.0, 11.0, 11.0, 9.0, 7.0, ShowClassIcon: false, ShowRankTitle: false, ShowTotalDamage: false, ShowSecondaryBadges: false, StackMetricsHorizontally: true), 
			_ => new OverlayDensityMetrics(40.0, 2.0, "10,0,8,0", 26.0, 6.0, 14.0, 13.0, 11.0, 16.0, ShowClassIcon: true, ShowRankTitle: true, ShowTotalDamage: true, ShowSecondaryBadges: true, StackMetricsHorizontally: false), 
		};
	}

	public static OverlayDensity Next(OverlayDensity current)
	{
		return current switch
		{
			OverlayDensity.Normal => OverlayDensity.Compact, 
			OverlayDensity.Compact => OverlayDensity.Micro, 
			_ => OverlayDensity.Normal, 
		};
	}

	public static string ShortLabel(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => "C", 
			OverlayDensity.Micro => "M", 
			_ => "N", 
		};
	}

	public static string DisplayName(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => "Compact", 
			OverlayDensity.Micro => "Micro", 
			_ => "Normal", 
		};
	}

	public static double EstimatedHeight(OverlayDensity density, int playerCount)
	{
		if (playerCount < 0)
		{
			playerCount = 0;
		}
		OverlayDensityMetrics overlayDensityMetrics = For(density);
		return 110.0 + (double)playerCount * (overlayDensityMetrics.RowHeight + overlayDensityMetrics.RowMargin * 2.0);
	}
}
