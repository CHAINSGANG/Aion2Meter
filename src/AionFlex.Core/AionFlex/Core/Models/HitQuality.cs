namespace AionFlex.Core.Models;

public readonly record struct HitQuality(bool Critical, HitDirection Direction, HitModifier Modifier, int ChainIndex, bool? MultiHitProc = null)
{
	public bool IsQualified
	{
		get
		{
			if (Direction != HitDirection.Unknown)
			{
				return Modifier != HitModifier.Unknown;
			}
			return false;
		}
	}

	public bool IsBack => Direction == HitDirection.Back;

	public bool IsFront => Direction == HitDirection.Front;

	public bool IsDouble => (Modifier & HitModifier.Double) != 0;

	public bool IsPerfect => (Modifier & HitModifier.Perfect) != 0;

	public bool HasChainIndex => ChainIndex > 0;

	public bool IsAdditionalHit => ChainIndex >= 2;

	public bool HasMultiHitData => MultiHitProc.HasValue;

	public bool IsMultiHit => MultiHitProc == true;

	public static readonly HitQuality Unknown = new HitQuality(Critical: false, HitDirection.Unknown, HitModifier.Unknown, 0);
}
