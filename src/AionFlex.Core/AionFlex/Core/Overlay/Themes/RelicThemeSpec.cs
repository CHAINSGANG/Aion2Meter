namespace AionFlex.Core.Overlay.Themes;

public sealed class RelicThemeSpec : OverlayThemeSpec
{
	public readonly record struct RowGeometry(double PadLeft, double PadRight, double PadBottom, double RankWidth, double ColumnGap, double SocketSize, double IconSize, double IconOffset, double GemSize, double GemCenterX, double GemCenterY, double GlowSize, double ChannelStartX, double ChannelHeight, double ChannelBottom, double WashCenterX, double WashCenterYFraction, double WashRadiusX, double WashRadiusY);

	private const double ListPadSide = 6.0;

	private const double ListPadBottom = 7.0;

	private const int ReorderDurationMs = 380;

	private const double S1X = 0.25;

	private const double S1Y = 0.8;

	private const double S2X = 0.3;

	private const double S2Y = 1.0;

	public const uint PlateBorder = 4281543199u;

	public const uint PlateBorderYou = 4291140437u;

	public const uint BevelTop = 318761150u;

	public const uint BevelTopYou = 536864958u;

	public const uint BevelBottom = 2785017856u;

	public const uint YouInnerRing = 1204132693u;

	public const uint RankText = 4288056182u;

	public const uint RankTextFirst = 4293644166u;

	public const uint NameText = 4292728000u;

	public const uint NameTextYou = 4294964178u;

	public const double GemBaseOffset = 0.4;

	public const double GlowMidOffset = 0.45;

	public const double ChannelBaseOffset = 0.45;

	public const double FlarePeakScale = 1.7;

	public const int FlarePeakMs = 315;

	public const int FlareTotalMs = 900;

	public const double FlareSplineX1 = 0.2;

	public const double FlareSplineY1 = 0.9;

	public const double FlareSplineX2 = 0.3;

	public const double FlareSplineY2 = 1.0;

	public override OverlayThemeId Id => OverlayThemeId.Relic;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(28.0, 2.0, 16.0, 13.0, 14.0, 10.5, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: true, 6.0, 0.0, 6.0, 7.0, ClipsRow: false, 380, 0.25, 0.8, 0.3, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(20.0, 2.0, 0.0, 11.0, 12.0, 9.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: true, 6.0, 0.0, 6.0, 7.0, ClipsRow: false, 380, 0.25, 0.8, 0.3, 1.0), 
			_ => new OverlayThemeMetrics(38.0, 3.0, 22.0, 13.0, 16.0, 10.5, ShowSubLine: true, ShowDamage: true, ShowIcon: true, MetricsHorizontal: false, 6.0, 0.0, 6.0, 7.0, ClipsRow: false, 380, 0.25, 0.8, 0.3, 1.0), 
		};
	}

	public static RowGeometry Geometry(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new RowGeometry(4.0, 8.0, 4.0, 16.0, 6.0, 20.0, 16.0, 2.0, 8.0, 18.0, 17.0, 16.0, 46.0, 3.0, 2.0, 36.0, 0.42, 64.0, 34.0), 
			OverlayDensity.Micro => new RowGeometry(4.0, 8.0, 3.0, 16.0, 6.0, 0.0, 0.0, 0.0, 6.0, 0.0, 0.0, 12.0, 22.0, 2.0, 1.0, 28.0, 0.42, 48.0, 20.0), 
			_ => new RowGeometry(4.0, 8.0, 8.0, 16.0, 6.0, 28.0, 22.0, 3.0, 10.0, 26.0, 25.0, 20.0, 54.0, 4.0, 2.0, 36.0, 0.42, 64.0, 34.0), 
		};
	}

	public static uint WashCore(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 66);
	}

	public static uint GemLight(uint c)
	{
		return ThemeColorMath.MixWhite(c, 0.45);
	}

	public static uint GemBase(uint c)
	{
		return ThemeColorMath.WithAlpha(c, byte.MaxValue);
	}

	public static uint GemDark(uint c)
	{
		return ThemeColorMath.MixBlack(c, 0.45);
	}

	public static uint GlowCore(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 230);
	}

	public static uint GlowMid(uint c)
	{
		return ThemeColorMath.WithAlpha(c, 102);
	}

	public static uint ChannelLight(uint c)
	{
		return ThemeColorMath.MixWhite(c, 0.7);
	}

	public static uint ChannelDark(uint c)
	{
		return ThemeColorMath.MixBlack(c, 0.6);
	}
}
