namespace AionFlex.Core.Overlay;

public static class OverlayFooterLayout
{
	public const double MinFooterWidth = 186.0;

	public const double WordmarkMinFooterWidth = 240.0;

	public const double EnergyChipAllowance = 72.0;

	public static bool ShowEnergyChip(double footerWidth, bool energyKnown)
	{
		if (energyKnown && double.IsFinite(footerWidth))
		{
			return footerWidth >= 258.0;
		}
		return false;
	}

	public static bool ShowWordmark(double footerWidth, bool energyKnown)
	{
		if (double.IsFinite(footerWidth))
		{
			return footerWidth >= 240.0 + (ShowEnergyChip(footerWidth, energyKnown) ? 72.0 : 0.0);
		}
		return false;
	}
}
