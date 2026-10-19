namespace AionFlex.Core.Overlay.Themes;

public static class OverlayThemePickerPolicy
{
	public static bool IsSelected(OverlayThemeId card, string? savedWire, bool? entitled)
	{
		return OverlayThemePolicy.Resolve(savedWire, entitled == true) == card;
	}

	public static bool ShowsProChip(OverlayThemeId card, bool? entitled)
	{
		if (!OverlayThemePolicy.IsFree(card))
		{
			return entitled != true;
		}
		return false;
	}
}
