using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay.Themes;

public static class OverlayThemeCatalog
{
	private static readonly OverlayThemeInfo[] Themes = new OverlayThemeInfo[8]
	{
		new OverlayThemeInfo(OverlayThemeId.Classic, "classic", "Classic", "The original gold meter."),
		new OverlayThemeInfo(OverlayThemeId.Aether, "aether", "Aether Glass", "Class-coloured crystal panes on quiet ice."),
		new OverlayThemeInfo(OverlayThemeId.Relic, "relic", "Daevanion Relic", "Gunmetal plates in a bronze setting, a cut gem per class."),
		new OverlayThemeInfo(OverlayThemeId.Abyss, "abyss", "Abyss Broadcast", "Esports scoreboard: hero party DPS, live sparklines."),
		new OverlayThemeInfo(OverlayThemeId.Asmodian, "asmodian", "Asmodian Nocturne", "Crimson smoke blades on obsidian."),
		new OverlayThemeInfo(OverlayThemeId.Elyos, "elyos", "Elyos Radiance", "Ivory crest plates with gilded wings."),
		new OverlayThemeInfo(OverlayThemeId.Prism, "prism", "Ghost Prism", "Nothing but light: floating text over hairline beams."),
		new OverlayThemeInfo(OverlayThemeId.Legion, "legion", "Legion Banners", "Class-coloured pill banners under a compact boss card.")
	};

	public static IReadOnlyList<OverlayThemeInfo> All => Themes;

	public static string ToWireId(OverlayThemeId id)
	{
		return Get(id).WireId;
	}

	public static OverlayThemeInfo Get(OverlayThemeId id)
	{
		OverlayThemeInfo[] themes = Themes;
		foreach (OverlayThemeInfo overlayThemeInfo in themes)
		{
			if (overlayThemeInfo.Id == id)
			{
				return overlayThemeInfo;
			}
		}
		return Themes[0];
	}

	public static bool TryParse(string? wire, out OverlayThemeId id)
	{
		id = OverlayThemeId.Classic;
		if (string.IsNullOrWhiteSpace(wire))
		{
			return false;
		}
		string b = wire.Trim();
		OverlayThemeInfo[] themes = Themes;
		foreach (OverlayThemeInfo overlayThemeInfo in themes)
		{
			if (string.Equals(overlayThemeInfo.WireId, b, StringComparison.OrdinalIgnoreCase))
			{
				id = overlayThemeInfo.Id;
				return true;
			}
		}
		return false;
	}
}
