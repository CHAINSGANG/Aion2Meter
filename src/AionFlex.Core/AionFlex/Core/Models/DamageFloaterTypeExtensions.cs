namespace AionFlex.Core.Models;

public static class DamageFloaterTypeExtensions
{
	public static bool IsCriticalHit(this DamageFloaterType type)
	{
		return HitQualityDecoder.DecodeFloaterType(type).Critical;
	}

	public static bool IsBackAttack(this DamageFloaterType type)
	{
		return HitQualityDecoder.DecodeFloaterType(type).Back;
	}

	public static bool IsDoubleHit(this DamageFloaterType type)
	{
		return HitQualityDecoder.DecodeFloaterType(type).IsDouble;
	}

	public static bool IsPerfectHit(this DamageFloaterType type)
	{
		return HitQualityDecoder.DecodeFloaterType(type).IsPerfect;
	}

	public static bool IsDamageEvent(this DamageFloaterType type)
	{
		if (type > DamageFloaterType.BackPerfectCritical_LastAdditionalHit && type != DamageFloaterType.Dot && type != DamageFloaterType.Reflect && type != DamageFloaterType.Falling)
		{
			return type == DamageFloaterType.MPBurn;
		}
		return true;
	}

	public static bool IsDefenseEvent(this DamageFloaterType type)
	{
		if ((uint)(type - 36) <= 6u || (uint)(type - 46) <= 5u)
		{
			return true;
		}
		return false;
	}

	public static bool IsRecoveryEvent(this DamageFloaterType type)
	{
		if (type >= DamageFloaterType.RecoverHP)
		{
			return type <= DamageFloaterType.RecoverHP_ByHPDrain;
		}
		return false;
	}

	public static bool IsAdditionalHit(this DamageFloaterType type)
	{
		if (type <= DamageFloaterType.BackPerfectCritical_LastAdditionalHit)
		{
			return HitQualityDecoder.DecodeFloaterType(type).IsAdditionalHit;
		}
		switch (type)
		{
		case DamageFloaterType.IronWall_AdditionalHit:
		case DamageFloaterType.IronWall_LastAdditionalHit:
		case DamageFloaterType.Restoration_AdditionalHit:
		case DamageFloaterType.Restoration_LastAdditionalHit:
		case DamageFloaterType.ShieldBlock_AdditionalHit:
		case DamageFloaterType.ShieldBlock_LastAdditionalHit:
		case DamageFloaterType.WeaponBlock_AdditionalHit:
		case DamageFloaterType.WeaponBlock_LastAdditionalHit:
			return true;
		default:
			return false;
		}
	}
}
