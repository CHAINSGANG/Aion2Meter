namespace AionFlex.Core.Overlay.Themes;

public sealed class AetherThemeSpec : OverlayThemeSpec
{
	public const int ReorderMs = 420;

	public const double GutterWidth = 4.0;

	public const double TrackRadius = 7.0;

	public const double CapWidth = 1.5;

	public const double CapMinFraction = 0.004;

	public const double PlateFraction = 0.46;

	public static readonly uint[] GlossArgb = new uint[6] { 1476395007u, 536870911u, 318767103u, 16777215u, 0u, 855638016u };

	public const uint TrackBaseArgb = 2651327252u;

	public const uint YouRingArgb = 3355443199u;

	public const uint IconDiscEdgeArgb = 4060481808u;

	public const double IconDiscEdgeOffset = 0.72;

	public const uint GoldArgb = 4293049687u;

	public const uint TextShadowArgb = 3338665984u;

	public const double SweepBandFraction = 0.38;

	public const double SweepSkewDeg = -20.0;

	public const double SweepFromBands = -1.6;

	public const double SweepToBands = 3.0;

	public const int SweepTravelMs = 1600;

	public const int SweepPeriodMs = 5000;

	public const int FlashPeakMs = 162;

	public const int FlashMs = 900;

	public override OverlayThemeId Id => OverlayThemeId.Aether;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(27.0, 2.0, 21.0, 13.0, 14.0, 9.5, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: true, 10.0, 0.0, 10.0, 10.0, ClipsRow: true, 420, 0.2, 0.7, 0.2, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(20.0, 2.0, 0.0, 11.0, 12.0, 9.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: false, 10.0, 0.0, 10.0, 10.0, ClipsRow: true, 420, 0.2, 0.7, 0.2, 1.0), 
			_ => new OverlayThemeMetrics(34.0, 3.0, 26.0, 13.0, 15.5, 9.5, ShowSubLine: true, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 10.0, 0.0, 10.0, 10.0, ClipsRow: true, 420, 0.2, 0.7, 0.2, 1.0), 
		};
	}

	public static double StackWidth(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 84, 
			OverlayDensity.Micro => 0, 
			_ => 46, 
		};
	}

	public static double DpsWidth(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 70, 
			OverlayDensity.Micro => 60, 
			_ => 76, 
		};
	}

	public static double[] GlossOffsets(double rowHeight)
	{
		double num = ((double.IsFinite(rowHeight) && rowHeight > 1.0) ? rowHeight : 1.0);
		double[] array = new double[6] { 0.0, 0.0, 0.46, 0.52, 0.52, 1.0 };
		array[1] = 1.0 / num;
		return array;
	}

	public static uint WashStart(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 43);
	}

	public static uint WashEnd(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 18);
	}

	public static uint TrackRing(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 41);
	}

	public static uint FillStart(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 112);
	}

	public static uint FillEnd(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 204);
	}

	public static (uint Start, uint Mid, uint End) IconFrame(uint c, bool isRankOne)
	{
		if (!isRankOne)
		{
			return (Start: 3221225471u, Mid: ThemeColorMath.WithAlpha(c, 242), End: ThemeColorMath.WithAlpha(c, 89));
		}
		return (Start: 4294964431u, Mid: 4293049687u, End: ThemeColorMath.WithAlpha(c, 153));
	}

	public static uint IconDiscCenter(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 77);
	}

	public static uint RankArgb(bool isRankOne)
	{
		if (!isRankOne)
		{
			return 3019176187u;
		}
		return 4293049687u;
	}

	public static int RankWeight(bool isRankOne)
	{
		if (!isRankOne)
		{
			return 600;
		}
		return 700;
	}

	public static uint NameArgb(bool isYou)
	{
		if (!isYou)
		{
			return 4294441983u;
		}
		return uint.MaxValue;
	}

	public static int NameWeight(bool isYou)
	{
		if (!isYou)
		{
			return 600;
		}
		return 700;
	}
}
