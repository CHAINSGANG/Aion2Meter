namespace AionFlex.Core.Overlay.Themes;

public static class OverlayThemeOptionPolicy
{
	public static bool IsControlledByTheme(OverlayThemeId theme, OverlayAppearanceOption option)
	{
		if (theme == OverlayThemeId.Classic)
		{
			return false;
		}
		if ((uint)(option - 3) <= 3u)
		{
			return true;
		}
		return false;
	}

	public static bool TreatsGoldAsClass(OverlayThemeId theme)
	{
		return theme != OverlayThemeId.Classic;
	}
}
