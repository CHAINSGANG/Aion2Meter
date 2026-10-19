namespace AionFlex.Core.Overlay.Themes;

public sealed class ElyosThemeSpec : OverlayThemeSpec
{
	public readonly record struct ElyosRowColors(uint PlateTop, uint PlateBottom, uint RimEdge, uint RimSecond, uint FillStart, uint FillEnd, uint BaseLine, uint BaseGlowTop, uint BaseGlowBottom, uint Inlay, uint InlayGlowStart, uint InlayGlowEnd, uint MedalRing, uint Rank, uint Name, uint NameShadow, uint MitIcon, uint MitText, uint Dps, uint DpsShadow, uint DpsUnit, uint Damage, uint Share);

	public const double MedalRingThickness = 1.5;

	public const double MedalImageInset = 2.0;

	public const double RowCornerRadius = 4.0;

	public const double DamageColumnWidth = 56.0;

	public const double NameCellMinWidthForMit = 80.0;

	public const double NameCellMinWidthForGlobe = 100.0;

	public override OverlayThemeId Id => OverlayThemeId.Elyos;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(26.0, 3.0, 20.0, 13.5, 15.0, 12.0, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 5.0, 0.0, 5.0, 5.0, ClipsRow: true, 500, 0.2, 0.75, 0.2, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(18.0, 2.0, 0.0, 11.0, 12.0, 10.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: false, 5.0, 0.0, 5.0, 5.0, ClipsRow: true, 500, 0.2, 0.75, 0.2, 1.0), 
			_ => new OverlayThemeMetrics(30.0, 3.0, 24.0, 13.5, 15.0, 12.0, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 5.0, 0.0, 5.0, 5.0, ClipsRow: true, 500, 0.2, 0.75, 0.2, 1.0), 
		};
	}

	public static double RankFontSize(bool isRankOne)
	{
		if (!isRankOne)
		{
			return 11.0;
		}
		return 12.5;
	}

	public static double MedalBoxSize(OverlayDensity density)
	{
		double iconSize = OverlayThemeSpecs.For(OverlayThemeId.Elyos).Metrics(density).IconSize;
		if (!(iconSize > 0.0))
		{
			return 0.0;
		}
		return iconSize + 3.0;
	}

	public static ElyosRowColors RowColors(uint classArgb, bool isRankOne, bool isYou)
	{
		uint num = ThemeColorMath.WithAlpha(classArgb, byte.MaxValue);
		uint plateTop = (isYou ? 4281214488u : 4294175706u);
		uint plateBottom = (isYou ? 4280030990u : 4293121723u);
		uint num2;
		if (isYou)
		{
			num2 = 4293050218u;
		}
		else
		{
			num2 = ((!isRankOne) ? 2365587455u : 4291402300u);
		}
		uint rimEdge = num2;
		uint num3 = (isRankOne ? ((!isYou) ? 3439325380u : 2365582516u) : 0u);
		uint rimSecond = num3;
		uint fillStart = ThemeColorMath.WithAlpha(num, isYou ? 0.42 : 0.8);
		uint fillEnd = ThemeColorMath.WithAlpha(num, isYou ? 0.18 : 0.55);
		uint baseLine = num;
		uint baseGlowTop = ThemeColorMath.Clear(num);
		uint baseGlowBottom = ThemeColorMath.WithAlpha(num, 0.6);
		uint inlay = num;
		uint inlayGlowStart = ThemeColorMath.WithAlpha(num, 0.85);
		uint inlayGlowEnd = ThemeColorMath.Clear(num);
		uint medalRing = num;
		uint rank;
		if (isRankOne)
		{
			rank = ((!isYou) ? 4288310036u : 4294234746u);
		}
		else
		{
			rank = ((!isYou) ? 4285224232u : 4293050218u);
		}
		return new ElyosRowColors(plateTop, plateBottom, rimEdge, rimSecond, fillStart, fillEnd, baseLine, baseGlowTop, baseGlowBottom, inlay, inlayGlowStart, inlayGlowEnd, medalRing, rank, isYou ? 4294965218u : 4279636490u, isYou ? 3003121664u : 2365587455u, isYou ? 4293050218u : 4286210352u, isYou ? 4293449646u : 4284369702u, isYou ? 4294965218u : 4279373319u, isYou ? 3003121664u : 0u, isYou ? 4293050218u : 4284369442u, isYou ? 4293449646u : 4281214228u, isYou ? 4293449646u : 4283054618u);
	}
}
