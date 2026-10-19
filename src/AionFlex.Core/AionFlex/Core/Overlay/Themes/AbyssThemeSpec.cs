using System;

namespace AionFlex.Core.Overlay.Themes;

public sealed class AbyssThemeSpec : OverlayThemeSpec
{
	public const uint Ink = 4278585357u;

	public const uint Plate = 3859220750u;

	public const uint Line = 402653183u;

	public const uint Mute = 2497898738u;

	public const uint Signal = 4294919730u;

	public const uint Text = 4294178297u;

	public const uint Up = 4281983114u;

	public const uint Down = 4294924872u;

	public const uint Gold = 4294035291u;

	public const uint GoldInk = 4279570698u;

	public const uint RankIdleBackground = 4279506977u;

	public const uint RankIdleText = 4292731626u;

	public const uint MetaText = 2834100471u;

	public const uint SecondaryText = 3018649847u;

	public const uint PerSecondText = 2666328311u;

	public const uint BarBackground = 167772159u;

	public const uint TextShadow = 3087007744u;

	public const uint IconDisc = 2651195405u;

	public const double RankColumnWidth = 22.0;

	public const double BlockSlantPx = 6.0;

	public const double EdgeWidthPx = 2.0;

	public const double EdgeHiddenAtOrBelow = 0.006;

	public const double FillMidOffset = 0.6;

	public const double DamageColumnWidth = 56.0;

	public const double ShareColumnWidth = 30.0;

	public const double DpsColumnWidth = 100.0;

	public const double CellGap = 6.0;

	public const double DamageColumnMin = 46.0;

	public const double DpsColumnMinPerFontPx = 4.5;

	public const double WhoMinShare = 0.4;

	public const byte FillStartAlpha = 140;

	public const byte FillMidAlpha = 87;

	public const byte FillEndAlpha = 46;

	public override OverlayThemeId Id => OverlayThemeId.Abyss;

	public override bool NeedsDpsHistory => true;

	public override bool NeedsRankDelta => true;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(26.0, 2.0, 19.0, 13.5, 17.0, 9.5, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: true, 4.0, 4.0, 4.0, 5.0, ClipsRow: true, 420, 0.2, 0.8, 0.2, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(20.0, 2.0, 0.0, 11.0, 13.0, 9.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: true, 4.0, 4.0, 4.0, 5.0, ClipsRow: true, 420, 0.2, 0.8, 0.2, 1.0), 
			_ => new OverlayThemeMetrics(34.0, 3.0, 24.0, 13.5, 21.0, 9.5, ShowSubLine: true, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 4.0, 4.0, 4.0, 5.0, ClipsRow: true, 420, 0.2, 0.8, 0.2, 1.0), 
		};
	}

	public static double BlockWidth(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 28, 
			OverlayDensity.Micro => 18, 
			_ => 34, 
		};
	}

	public static double IconOffsetX(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 3, 
			OverlayDensity.Micro => 0, 
			_ => 4, 
		};
	}

	public static (double X, double Y)[] BlockPolygon(OverlayDensity density)
	{
		double num = BlockWidth(density);
		double rowHeight = OverlayThemeSpecs.For(OverlayThemeId.Abyss).Metrics(density).RowHeight;
		return new (double, double)[4]
		{
			(0.0, 0.0),
			(num, 0.0),
			(num - 6.0, rowHeight),
			(0.0, rowHeight)
		};
	}

	public static double LegendPadLeft(OverlayDensity density)
	{
		return 26.0 + BlockWidth(density) - 6.0 + 12.0;
	}

	public static uint BlockFill(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, byte.MaxValue);
	}

	public static uint FillStart(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, 140);
	}

	public static uint FillMid(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, 87);
	}

	public static uint FillEnd(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, 46);
	}

	public static uint SparkStroke(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, 0.9);
	}

	public static (uint Background, uint Foreground) RankChip(bool isRankOne, bool isYou)
	{
		if (!isRankOne)
		{
			if (!isYou)
			{
				return (Background: 4279506977u, Foreground: 4292731626u);
			}
			return (Background: uint.MaxValue, Foreground: 4278585357u);
		}
		return (Background: 4294035291u, Foreground: 4279570698u);
	}

	public static AbyssShareSlot ShareSlot(bool showShare, bool showDamage, bool metricsHorizontal)
	{
		if (showShare)
		{
			if (!showDamage || metricsHorizontal)
			{
				return AbyssShareSlot.OwnColumn;
			}
			return AbyssShareSlot.UnderDamage;
		}
		return AbyssShareSlot.Hidden;
	}

	public static (double Damage, double Share, double Dps) CellColumns(double cellsWidth, double dpsFontSize, bool showDamage, bool shareColumn, bool showDps)
	{
		double num = (showDamage ? 56.0 : 0.0);
		double num2 = (shareColumn ? 30.0 : 0.0);
		double num3 = (showDps ? 100.0 : 0.0);
		if (!double.IsFinite(cellsWidth) || cellsWidth <= 0.0)
		{
			return (Damage: num, Share: num2, Dps: num3);
		}
		double num4 = 6.0 * (double)((showDamage ? 1 : 0) + (shareColumn ? 1 : 0) + (showDps ? 1 : 0));
		double num5 = 0.4 * cellsWidth - (cellsWidth - num4 - num - num2 - num3);
		if (num5 <= 0.0)
		{
			return (Damage: num, Share: num2, Dps: num3);
		}
		if (showDps)
		{
			double num6 = ((double.IsFinite(dpsFontSize) && dpsFontSize > 0.0) ? Math.Min(100.0, 4.5 * dpsFontSize) : 100.0);
			double num7 = Math.Min(num5, num3 - num6);
			num3 -= num7;
			num5 -= num7;
		}
		if (showDamage && num5 > 0.0)
		{
			num -= Math.Min(num5, 10.0);
		}
		return (Damage: num, Share: num2, Dps: num3);
	}

	public static AbyssCellLayout CellLayout(double cellsWidth, double dpsFontSize, bool showDamage, bool showShare, bool showDps, bool metricsHorizontal)
	{
		AbyssCellLayout abyssCellLayout = Columns(cellsWidth, dpsFontSize, showDamage, showShare, showDps, metricsHorizontal);
		if (showDamage && Overflows(abyssCellLayout, cellsWidth))
		{
			abyssCellLayout = Columns(cellsWidth, dpsFontSize, showDamage: false, showShare, showDps, metricsHorizontal);
		}
		if (showShare && Overflows(abyssCellLayout, cellsWidth))
		{
			abyssCellLayout = Columns(cellsWidth, dpsFontSize, showDamage: false, showShare: false, showDps, metricsHorizontal);
		}
		return abyssCellLayout;
	}

	private static AbyssCellLayout Columns(double cellsWidth, double dpsFontSize, bool showDamage, bool showShare, bool showDps, bool metricsHorizontal)
	{
		AbyssShareSlot abyssShareSlot = ShareSlot(showShare, showDamage, metricsHorizontal);
		var (damage, share, dps) = CellColumns(cellsWidth, dpsFontSize, showDamage, abyssShareSlot == AbyssShareSlot.OwnColumn, showDps);
		return new AbyssCellLayout(damage, share, dps, abyssShareSlot);
	}

	private static bool Overflows(AbyssCellLayout layout, double cellsWidth)
	{
		if (double.IsFinite(cellsWidth) && cellsWidth > 0.0)
		{
			return layout.Width > cellsWidth + 1E-09;
		}
		return false;
	}

	public static bool ShowsEdge(double barFraction)
	{
		if (double.IsFinite(barFraction))
		{
			return barFraction > 0.006;
		}
		return false;
	}
}
