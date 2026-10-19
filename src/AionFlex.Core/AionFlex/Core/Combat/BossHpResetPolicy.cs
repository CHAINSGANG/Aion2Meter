using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class BossHpResetPolicy
{
	public static bool IsBackToFull(HpReadingSource source, uint reading, uint? previousWireHp, uint framedSpawnMaxHp, bool reachedZeroHp, uint minBossMaxHp)
	{
		if (source == HpReadingSource.HpUpdate008D && framedSpawnMaxHp >= minBossMaxHp && reading == framedSpawnMaxHp && previousWireHp.HasValue && previousWireHp.Value < framedSpawnMaxHp)
		{
			return !reachedZeroHp;
		}
		return false;
	}

	public static bool IsTrackedBoss(int entityId, int trackedBossEntityId)
	{
		if (trackedBossEntityId > 0)
		{
			return entityId == trackedBossEntityId;
		}
		return false;
	}

	public static bool IsAboveCapWithinFramedSpawnMax(uint reading, uint cap, uint framedSpawnMaxHp)
	{
		if (reading > cap && framedSpawnMaxHp != 0)
		{
			return reading <= framedSpawnMaxHp;
		}
		return false;
	}
}
