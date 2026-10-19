using System;

namespace AionFlex.Core.Combat;

public static class BossHpResetRouter
{
	public static bool Route(BossHpResetEvent reset, int trackedBossEntityId, Action<BossHpResetEvent, int> keepMeter, Action<int> cancelPendingSave, Action<BossHpResetEvent> resetMeter)
	{
		ArgumentNullException.ThrowIfNull(keepMeter, "keepMeter");
		ArgumentNullException.ThrowIfNull(cancelPendingSave, "cancelPendingSave");
		ArgumentNullException.ThrowIfNull(resetMeter, "resetMeter");
		if (!BossHpResetPolicy.IsTrackedBoss(reset.EntityId, trackedBossEntityId))
		{
			keepMeter(reset, trackedBossEntityId);
			return false;
		}
		cancelPendingSave(reset.EntityId);
		resetMeter(reset);
		return true;
	}
}
