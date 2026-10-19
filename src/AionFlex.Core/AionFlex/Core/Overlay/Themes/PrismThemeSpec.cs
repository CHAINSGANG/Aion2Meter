using System;

namespace AionFlex.Core.Overlay.Themes;

public sealed class PrismThemeSpec : OverlayThemeSpec
{
	public const uint Ink = 4294244347u;

	public const uint Ink2 = 3773756410u;

	public const uint Ink3 = 3018255862u;

	public const uint Void = 4278519821u;

	public const uint TextOutline = 3640985613u;

	public const double TextOutlineThickness = 2.4;

	public const double SmallTextOutlineThickness = 1.0;

	public const double OutlineMinFontSize = 11.0;

	public const double FullOutlineMinFontSize = 14.0;

	public const double SilhouetteOffsetY = 0.0;

	public const double PanelCornerRadius = 6.0;

	public static readonly (uint Argb, double Offset)[] ScrimStops = new (uint, double)[3]
	{
		(2147813389u, 0.0),
		(1459947533u, 0.6),
		(1107625997u, 1.0)
	};

	public const uint HpTrack = 1929709581u;

	public const uint HpTrackRing = 1074071565u;

	public const uint HpFillStart = 4076140539u;

	public const uint HpFillEnd = 3438606331u;

	public const double HpTrackHeight = 2.0;

	public const double HpRingHeight = 4.0;

	public const uint LegendBorder = 620034043u;

	public const double RankColumn = 16.0;

	public const double IconColumn = 20.0;

	public const double DamageColumn = 50.0;

	public const double ShareColumn = 42.0;

	public const double DpsColumn = 70.0;

	public const double ColumnGap = 6.0;

	public static readonly (uint Argb, double Offset)[] YouPlateStops = new (uint, double)[3]
	{
		(2651129869u, 0.0),
		(1711605773u, 0.7),
		(939853837u, 1.0)
	};

	public const double YouBleed = 12.0;

	public const double YouPlateTopBleed = 2.0;

	public const double NotchWidth = 3.0;

	public const double NotchTopInset = 2.0;

	public const double NotchBottomInset = 4.0;

	public const uint NotchBacking = 3422881805u;

	public const double NotchBackingWidth = 5.0;

	public const double BeamLeft = 22.0;

	public const uint BeamTrack = 2349139981u;

	public const double BeamTrackRadius = 1.0;

	public const double BeamTrackBottom = 1.0;

	public static readonly (uint Argb, double Offset)[] GlintStops = new (uint, double)[4]
	{
		(4294955642u, 0.0),
		(uint.MaxValue, 0.45),
		(4287557887u, 0.7),
		(4291404543u, 1.0)
	};

	public const double GlintSize = 8.0;

	public const double GlintBackingSize = 10.0;

	public const double GlintCentreOffset = 1.0;

	public const double MicroGlintSize = 6.0;

	public const double MicroGlintBackingSize = 8.0;

	public const uint GlintBacking = 2349139981u;

	public const double FlareWidth = 44.0;

	public const double FlareHeight = 25.0;

	public const double FlareClipBleed = 6.0;

	public static readonly (uint Argb, double Offset)[] FlareStops = new (uint, double)[4]
	{
		(16777215u, 0.0),
		(4076863487u, 0.55),
		(3012489471u, 0.75),
		(13214463u, 1.0)
	};

	public const int FlareMs = 900;

	public const int FlareFadeInMs = 135;

	public const int FlareFadeOutStartMs = 765;

	public static readonly (double X1, double Y1, double X2, double Y2) FlareSpline = (X1: 0.3, Y1: 0.6, X2: 0.2, Y2: 1.0);

	public const uint IconDiscCentre = 3338995725u;

	public const uint IconDiscEdge = 2651129869u;

	public override OverlayThemeId Id => OverlayThemeId.Prism;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(28.0, 0.0, 20.0, 13.0, 14.0, 12.0, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 12.0, 0.0, 12.0, 10.0, ClipsRow: false, 380, 0.22, 0.8, 0.26, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(20.0, 0.0, 0.0, 11.0, 12.0, 10.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: false, 12.0, 0.0, 12.0, 10.0, ClipsRow: false, 380, 0.22, 0.8, 0.26, 1.0), 
			_ => new OverlayThemeMetrics(34.0, 0.0, 22.0, 13.0, 15.0, 12.0, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 12.0, 0.0, 12.0, 10.0, ClipsRow: false, 380, 0.22, 0.8, 0.26, 1.0), 
		};
	}

	public static double BeamZoneHeight(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 6, 
			OverlayDensity.Micro => 4, 
			_ => 7, 
		};
	}

	public static double TrackHeight(OverlayDensity density)
	{
		return (density == OverlayDensity.Micro) ? 3 : 5;
	}

	public static double CoreHeight(OverlayDensity density, bool isRankOne)
	{
		if (!isRankOne)
		{
			return (density == OverlayDensity.Micro) ? 2 : 3;
		}
		return TrackHeight(density);
	}

	public static double GlowHeight(OverlayDensity density)
	{
		return (density == OverlayDensity.Micro) ? 7 : 11;
	}

	public static uint BeamCore(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, byte.MaxValue);
	}

	public static (uint Argb, double Offset)[] GlowStops(uint classArgb)
	{
		return new (uint, double)[3]
		{
			(ThemeColorMath.Clear(classArgb), 0.0),
			(ThemeColorMath.WithAlpha(classArgb, 0.36), 0.5),
			(ThemeColorMath.Clear(classArgb), 1.0)
		};
	}

	public static (uint Argb, double Offset)[] TipStops(uint classArgb)
	{
		return new (uint, double)[5]
		{
			(BeamCore(classArgb), 0.0),
			(4294955642u, 0.278),
			(uint.MaxValue, 0.528),
			(4287557887u, 0.722),
			(4291404543u, 1.0)
		};
	}

	public static double GlintSizeFor(OverlayDensity density)
	{
		if (density != OverlayDensity.Micro)
		{
			return 8.0;
		}
		return 6.0;
	}

	public static double GlintBackingSizeFor(OverlayDensity density)
	{
		if (density != OverlayDensity.Micro)
		{
			return 10.0;
		}
		return 8.0;
	}

	public static double GlintOffset(double size)
	{
		return 1.0 - size / 2.0;
	}

	public static double FlareX(double progress, double trackWidth)
	{
		double num = (double.IsNaN(progress) ? 0.0 : Math.Clamp(progress, 0.0, 1.0));
		double num2 = (double.IsFinite(trackWidth) ? Math.Max(0.0, trackWidth) : 0.0);
		return -44.0 + num * (num2 + 44.0);
	}

	public static uint RankInk(bool isRankOne, bool isYou)
	{
		if (!(isRankOne | isYou))
		{
			return 3018255862u;
		}
		return 4294244347u;
	}

	public static uint NameInk(bool isRankOne, bool isYou)
	{
		if (!(isRankOne | isYou))
		{
			return 3773756410u;
		}
		return 4294244347u;
	}

	public static int NameWeight(bool isYou)
	{
		if (!isYou)
		{
			return 600;
		}
		return 700;
	}

	public static double DpsFontSize(OverlayDensity density, bool isRankOne)
	{
		if (isRankOne)
		{
			return (density == OverlayDensity.Micro) ? 13 : 16;
		}
		return OverlayThemeSpecs.For(OverlayThemeId.Prism).Metrics(density).DpsFontSize;
	}

	public static bool IsOutlined(double fontSize)
	{
		return fontSize >= 11.0;
	}

	public static double OutlineThicknessFor(double fontSize)
	{
		if (!(fontSize >= 14.0))
		{
			if (!IsOutlined(fontSize))
			{
				return 0.0;
			}
			return 1.0;
		}
		return 2.4;
	}

	public static double CentredBleed(OverlayDensity density, double size)
	{
		return (size - TrackHeight(density)) / 2.0;
	}
}
