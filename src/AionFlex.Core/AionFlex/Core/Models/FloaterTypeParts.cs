namespace AionFlex.Core.Models;

public readonly record struct FloaterTypeParts(int HitIndex, bool Critical, bool Back, HitModifier Modifier)
{
	public bool IsDouble => (Modifier & HitModifier.Double) != 0;

	public bool IsPerfect => (Modifier & HitModifier.Perfect) != 0;

	public bool IsAdditionalHit => HitIndex != 0;
}
