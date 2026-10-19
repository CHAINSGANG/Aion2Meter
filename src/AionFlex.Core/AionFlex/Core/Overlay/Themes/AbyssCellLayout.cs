namespace AionFlex.Core.Overlay.Themes;

public readonly record struct AbyssCellLayout(double Damage, double Share, double Dps, AbyssShareSlot Slot)
{
	public double Width => Damage + Share + Dps + 6.0 * (double)(((Damage > 0.0) ? 1 : 0) + ((Share > 0.0) ? 1 : 0) + ((Dps > 0.0) ? 1 : 0));
}
