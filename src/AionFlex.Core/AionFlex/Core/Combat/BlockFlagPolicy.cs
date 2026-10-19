using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class BlockFlagPolicy
{
	public const int ShieldBlockBit = 1;

	public const int WeaponBlockBit = 2;

	public const int IncomingBlockMask = 3;

	public const int LegacyBlockMask = 134;

	public const int ProvenBlockClass = 2;

	public const int MaxDirectionValue = 2;

	public static bool IsNpcHit(bool actorIsKnownPlayer, int skillCode)
	{
		if (!actorIsKnownPlayer)
		{
			return JobClassExtensions.FromSkillCode(skillCode) == JobClass.Unknown;
		}
		return false;
	}

	public static bool IsBlocked(bool actorIsKnownPlayer, bool targetIsKnownPlayer, int skillCode, int switchVar, int flagByte, int directionByte)
	{
		bool flag = HasProvenLayout(switchVar, directionByte);
		if (IsNpcHit(actorIsKnownPlayer, skillCode))
		{
			if (flag)
			{
				return (flagByte & 3) != 0;
			}
			return false;
		}
		if ((!actorIsKnownPlayer & targetIsKnownPlayer) && !flag)
		{
			return false;
		}
		return (flagByte & 0x86) != 0;
	}

	public static bool HasProvenLayout(int switchVar, int directionByte)
	{
		if ((switchVar & 3) == 2)
		{
			if (directionByte >= 0)
			{
				return directionByte <= 2;
			}
			return false;
		}
		return false;
	}
}
