namespace AionFlex.Core.Combat;

public static class EntityLifePolicy
{
	public const int DeathRemovalReason = 3;

	public static long? NewLifeCutoff(long? deathRemovalMs, bool currentLifeReachedZeroHp, long appearMs)
	{
		if (deathRemovalMs.HasValue)
		{
			return deathRemovalMs.Value;
		}
		if (!currentLifeReachedZeroHp)
		{
			return null;
		}
		return appearMs;
	}
}
