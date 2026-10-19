using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class BossLabelPolicy
{
	public static bool IsBossCandidateHit(int skillCode, bool isHeal, bool actorIsConfirmedPlayer, bool actorIsLocalPlayer)
	{
		if (!isHeal)
		{
			if (!(actorIsConfirmedPlayer | actorIsLocalPlayer))
			{
				return JobClassExtensions.FromSkillCode(skillCode) != JobClass.Unknown;
			}
			return true;
		}
		return false;
	}

	public static bool ProvesFriendlyTarget(int skillCode, bool isHeal, bool actorIsConfirmedPlayer, bool actorIsLocalPlayer)
	{
		if (isHeal)
		{
			if (!(actorIsConfirmedPlayer | actorIsLocalPlayer))
			{
				return JobClassExtensions.FromSkillCode(skillCode) != JobClass.Unknown;
			}
			return true;
		}
		return false;
	}

	public static bool MayApplyGenericLabel(int dealtEventCount, int topDealtSkillCode)
	{
		if (dealtEventCount > 0)
		{
			return JobClassExtensions.FromSkillCode(topDealtSkillCode) == JobClass.Unknown;
		}
		return true;
	}
}
