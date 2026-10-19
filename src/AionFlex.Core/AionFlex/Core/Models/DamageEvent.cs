using System;

namespace AionFlex.Core.Models;

public sealed class DamageEvent
{
	public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public int ActorId { get; init; }

	public int TargetId { get; init; }

	public int Damage { get; init; }

	public int SkillCode { get; init; }

	public int RawSkillCode { get; init; }

	public int Type { get; init; }

	public int MultiHitCount { get; init; }

	public int MultiHitDamage { get; init; }

	public int TotalDamage => Damage;

	public int Flag { get; init; }

	public int SwitchVariable { get; init; }

	public int Loop { get; init; }

	public int Unknown { get; init; }

	public int EventCounter { get; init; }

	public SpecialDamageFlags SpecialFlags { get; init; }

	public bool SpecialFlagsMeasured { get; init; }

	public bool IsBlocked { get; init; }

	public bool IsDoT { get; init; }

	public bool IsPassive { get; init; }

	public bool IsHeal { get; init; }

	public DamageFloaterType FloaterType
	{
		get
		{
			if (!Enum.IsDefined(typeof(DamageFloaterType), Type))
			{
				return DamageFloaterType.Normal;
			}
			return (DamageFloaterType)Type;
		}
	}

	public HitDirection Direction { get; init; }

	public HitModifier Modifier { get; init; }

	public int ChainIndex { get; init; }

	public bool? MultiHitProc { get; init; }

	public HitQuality Quality => new HitQuality(IsCritical, Direction, Modifier, ChainIndex, MultiHitProc);

	public bool IsCritical => HitQualityDecoder.DecodeFloaterType(Type).Critical;

	public bool IsBackAttack => Direction == HitDirection.Back;

	public bool IsDoubleHit => (Modifier & HitModifier.Double) != 0;

	public bool IsPerfectHit => (Modifier & HitModifier.Perfect) != 0;

	public bool IsDefenseEvent => FloaterType.IsDefenseEvent();

	public override string ToString()
	{
		string text = $"DamageEvent[Actor={ActorId}, Target={TargetId}, Skill={SkillCode}, Damage={Damage}, Crit={IsCritical}, DoT={IsDoT}, Flags={SpecialFlags}";
		if (MultiHitCount > 0)
		{
			text += $", MultiHit={MultiHitCount}x{MultiHitDamage}";
		}
		return text + "]";
	}
}
