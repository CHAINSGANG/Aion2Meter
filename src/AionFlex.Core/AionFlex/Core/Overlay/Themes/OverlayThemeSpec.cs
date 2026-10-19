namespace AionFlex.Core.Overlay.Themes;

public abstract class OverlayThemeSpec
{
	public abstract OverlayThemeId Id { get; }

	public virtual uint FallbackClassArgb => 4291535157u;

	public virtual bool NeedsDpsHistory => false;

	public virtual bool NeedsRankDelta => false;

	public virtual bool RowsStackTopAbove => false;

	public abstract OverlayThemeMetrics Metrics(OverlayDensity density);
}
