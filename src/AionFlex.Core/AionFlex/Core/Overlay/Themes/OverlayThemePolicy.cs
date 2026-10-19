namespace AionFlex.Core.Overlay.Themes;

public static class OverlayThemePolicy
{
	public const OverlayThemeId Default = OverlayThemeId.Legion;

	public static bool IsFree(OverlayThemeId theme)
	{
		if (theme == OverlayThemeId.Classic || (uint)(theme - 6) <= 1u)
		{
			return true;
		}
		return false;
	}

	public static OverlayThemeId Resolve(string? savedWire, bool entitled)
	{
		if (!OverlayThemeCatalog.TryParse(savedWire, out var id))
		{
			return OverlayThemeId.Legion;
		}
		if (!entitled && !IsFree(id))
		{
			return OverlayThemeId.Legion;
		}
		return id;
	}

	public static OverlayThemeId WithLoadFailure(OverlayThemeId resolved, OverlayThemeId? failedToLoad)
	{
		if (failedToLoad.HasValue)
		{
			OverlayThemeId valueOrDefault = failedToLoad.GetValueOrDefault();
			if (valueOrDefault == resolved)
			{
				return OverlayThemeId.Classic;
			}
		}
		return resolved;
	}

	public static bool UsesCustomColours(bool entitled, OverlayThemeId renderedTheme)
	{
		if (entitled)
		{
			return renderedTheme == OverlayThemeId.Classic;
		}
		return false;
	}
}
