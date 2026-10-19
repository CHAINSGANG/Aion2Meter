using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class FightMetaStamp
{
	public const int WipeRuleVersion = 1;

	public const string KillSignalZeroHp = "zeroHp";

	public const string KillSignalDamage90 = "damage90";

	public const string KillSignalNone = "none";

	public static string KillSignal(bool reachedZeroHp, bool killedOnDamage)
	{
		if (!reachedZeroHp)
		{
			if (!killedOnDamage)
			{
				return "none";
			}
			return "damage90";
		}
		return "zeroHp";
	}

	public static FightMeta Build(bool reachedZeroHp, bool killedOnDamage, long? trustedMaxHp, long observedMaxHp, string contributionBasis, long contributionDenominator, long? battleTimeClampedFromMs, int? wipesBefore, bool bossOnlyMode, int entityLifeGeneration)
	{
		bool flag = trustedMaxHp.HasValue && trustedMaxHp.GetValueOrDefault() > 0;
		return new FightMeta
		{
			KillSignal = KillSignal(reachedZeroHp, killedOnDamage),
			BossMaxHp = (flag ? trustedMaxHp : ((observedMaxHp > 0) ? new long?(observedMaxHp) : ((long?)null))),
			BossHpTrusted = ((flag || observedMaxHp > 0) ? new bool?(flag) : ((bool?)null)),
			ContributionBasis = contributionBasis,
			ContributionDenominator = ((contributionDenominator > 0) ? new long?(contributionDenominator) : ((long?)null)),
			BattleTimeClampedFromMs = battleTimeClampedFromMs,
			WipeCount = wipesBefore,
			AttemptIndex = wipesBefore + 1,
			WipeRuleVersion = ((!wipesBefore.HasValue) ? ((int?)null) : new int?(1)),
			TargetMode = (bossOnlyMode ? "bossOnly" : "allTargets"),
			EntityLifeGeneration = ((entityLifeGeneration >= 0) ? new int?(entityLifeGeneration) : ((int?)null))
		};
	}
}
