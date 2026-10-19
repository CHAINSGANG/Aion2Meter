namespace AionFlex.Core.Overlay.Themes;

public static class OverlayThemeSpecs
{
	private static readonly OverlayThemeSpec Aether = new AetherThemeSpec();

	private static readonly OverlayThemeSpec Relic = new RelicThemeSpec();

	private static readonly OverlayThemeSpec Abyss = new AbyssThemeSpec();

	private static readonly OverlayThemeSpec Asmodian = new AsmodianThemeSpec();

	private static readonly OverlayThemeSpec Elyos = new ElyosThemeSpec();

	private static readonly OverlayThemeSpec Prism = new PrismThemeSpec();

	private static readonly OverlayThemeSpec Legion = new LegionThemeSpec();

	public static OverlayThemeSpec? For(OverlayThemeId id)
	{
		return id switch
		{
			OverlayThemeId.Aether => Aether, 
			OverlayThemeId.Relic => Relic, 
			OverlayThemeId.Abyss => Abyss, 
			OverlayThemeId.Asmodian => Asmodian, 
			OverlayThemeId.Elyos => Elyos, 
			OverlayThemeId.Prism => Prism, 
			OverlayThemeId.Legion => Legion, 
			_ => null, 
		};
	}
}
