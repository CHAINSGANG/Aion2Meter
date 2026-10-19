using System;

namespace AionFlex.Core.Overlay.Themes;

public sealed class AsmodianThemeSpec : OverlayThemeSpec
{
	public const uint Obsidian = 4278847248u;

	public const uint Obsidian2 = 4279503901u;

	public const uint Bone = 4294044406u;

	public const uint Mist = 4289963714u;

	public const uint Dim = 4287266458u;

	public const uint Crimson = 4291831871u;

	public const uint CrimsonDeep = 4284156957u;

	public const uint Ember = 4294937149u;

	public const uint EmberHot = 4294955146u;

	public const uint DpsText = uint.MaxValue;

	public const uint RowSecondary = 2666654454u;

	public const uint DpsSuffix = 2582768374u;

	public const uint MitText = 4291410175u;

	public const uint TextShadow = 3858759680u;

	public const double EmberWidth = 26.0;

	public const double EdgeStroke = 3.0;

	public const double HiltGlowReach = 115.0;

	public const double YouPlateWidth = 30.0;

	public const double MinFillFraction = 0.04;

	public const double StatsLineHeightEm = 1.05;

	public const double GlobeMinNameColumn = 60.0;

	public const double GlobeMinNameColumnYou = 95.0;

	private static readonly AsmodianThemeSpec Spec = new AsmodianThemeSpec();

	public static readonly (uint Argb, double Offset)[] RankOneEdgeStops = new (uint, double)[3]
	{
		(4294955146u, 0.0),
		(4294937149u, 0.45),
		(4291831871u, 1.0)
	};

	public static readonly (uint Start, uint End) EmberGlow = (Start: 2365557309u, End: 16747069u);

	public override OverlayThemeId Id => OverlayThemeId.Asmodian;

	public override bool RowsStackTopAbove => true;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(27.0, 3.0, 22.0, 15.0, 15.0, 11.0, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 10.0, 0.0, 10.0, 10.0, ClipsRow: false, 320, 0.2, 0.8, 0.2, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(18.0, 2.0, 0.0, 11.0, 12.0, 9.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: false, 10.0, 0.0, 10.0, 10.0, ClipsRow: false, 320, 0.2, 0.8, 0.2, 1.0), 
			_ => new OverlayThemeMetrics(33.0, 3.0, 26.0, 15.0, 17.0, 11.0, ShowSubLine: true, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 10.0, 0.0, 10.0, 10.0, ClipsRow: false, 320, 0.2, 0.8, 0.2, 1.0), 
		};
	}

	public static double Slant(OverlayDensity density)
	{
		return (density == OverlayDensity.Micro) ? 6 : 10;
	}

	public static double FillSkewDeg(OverlayDensity density)
	{
		return ThemeGeometryMath.SkewAngleDeg(Slant(density), Spec.Metrics(density).RowHeight);
	}

	public static double EdgeWidth(OverlayDensity density)
	{
		return 3.0 + Slant(density);
	}

	public static double RankFontSize(OverlayDensity density)
	{
		return (density == OverlayDensity.Micro) ? 10 : 12;
	}

	public static double ShareFontSize(OverlayDensity density)
	{
		return (density == OverlayDensity.Micro) ? 9 : 10;
	}

	public static double DpsSuffixFontSize(OverlayDensity density)
	{
		return (density == OverlayDensity.Micro) ? 8 : 10;
	}

	public static double NameLineHeight(OverlayDensity density)
	{
		return Spec.Metrics(density).NameFontSize;
	}

	public static double DamageLineHeight(OverlayDensity density)
	{
		return Spec.Metrics(density).SecondaryFontSize * 1.05;
	}

	public static double ShareLineHeight(OverlayDensity density)
	{
		return ShareFontSize(density) * 1.05;
	}

	public static bool ShowsGlobe(double nameColumnWidth, bool isYou)
	{
		if (double.IsFinite(nameColumnWidth) && !(nameColumnWidth <= 0.0))
		{
			return nameColumnWidth >= (isYou ? 95.0 : 60.0);
		}
		return true;
	}

	public static bool ShowsSubLine(bool densityShowsSubLine, bool hasCombatPower, bool showMitigation, bool hasBlock)
	{
		if (densityShowsSubLine)
		{
			if (!hasCombatPower)
			{
				return showMitigation & hasBlock;
			}
			return true;
		}
		return false;
	}

	public static double FillScale(double fraction)
	{
		if (!double.IsFinite(fraction))
		{
			return 0.0;
		}
		return Math.Clamp(fraction, 0.04, 1.0);
	}

	public static double TipX(double fraction, double trackWidth)
	{
		return FillScale(fraction) * Math.Max(0.0, double.IsFinite(trackWidth) ? trackWidth : 0.0);
	}

	public static double EdgeOffsetX(double fraction, double trackWidth)
	{
		return TipX(fraction, trackWidth) - 3.0;
	}

	public static double EmberOffsetX(double fraction, double trackWidth, double slant)
	{
		return TipX(fraction, trackWidth) + slant - 26.0;
	}

	public static double HiltGlowWidth(double fraction, double trackWidth, double slant)
	{
		return Math.Min(115.0 + slant, TipX(fraction, trackWidth) + slant);
	}

	public static (uint Argb, double Offset)[] FillStops(uint classArgb)
	{
		return new (uint, double)[4]
		{
			(ThemeColorMath.WithAlpha(classArgb, 0.7), 0.0),
			(ThemeColorMath.WithAlpha(classArgb, 0.44), 0.24),
			(ThemeColorMath.WithAlpha(classArgb, 0.24), 0.64),
			(ThemeColorMath.WithAlpha(classArgb, 0.32), 1.0)
		};
	}

	public static (uint Start, uint End) HiltGlow(uint classArgb)
	{
		return (Start: ThemeColorMath.WithAlpha(classArgb, 0.5), End: ThemeColorMath.Clear(classArgb));
	}

	public static (uint Top, uint Bottom) Blade(bool isYou)
	{
		if (!isYou)
		{
			return (Top: 3861387830u, Bottom: 3943434258u);
		}
		return (Top: 4064424022u, Bottom: 4061531680u);
	}

	public static uint EdgeSolid(uint classArgb)
	{
		return ThemeColorMath.WithAlpha(classArgb, byte.MaxValue);
	}

	public static uint RankText(bool isRankOne, bool isYou)
	{
		if (!isYou)
		{
			if (!isRankOne)
			{
				return 4294044406u;
			}
			return 4294955146u;
		}
		return 4278847248u;
	}

	public static uint? RankShadow(bool isYou)
	{
		if (!isYou)
		{
			return 3858759680u;
		}
		return null;
	}
}
