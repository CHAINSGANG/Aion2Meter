using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Models;

public sealed class SkillMetadata
{
	public string Type { get; init; } = "Active";

	public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();

	public bool IsStigma { get; init; }

	public int ChainPrevCode { get; init; }

	public bool IsChainFollowUp => ChainPrevCode != 0;

	public string DamageType { get; init; } = "None";

	public string Disposition { get; init; } = "Negative";

	public bool IsPassive => Type == "Passive";

	public bool IsSpawn => Categories.Contains("Spawn");

	public bool IsBuff => Categories.Contains("Buff");

	public bool IsAttack => Categories.Contains("Attack");

	public bool IsHeal => Categories.Contains("Heal");

	public bool IsDebuff => Categories.Contains("Debuff");

	public bool CanProduceGhostDamage
	{
		get
		{
			if (!IsPassive && (!IsBuff || IsAttack))
			{
				return IsSpawn;
			}
			return true;
		}
	}

	public SkillRole Role
	{
		get
		{
			if (IsHeal && !IsAttack)
			{
				return SkillRole.Heal;
			}
			if (IsAttack)
			{
				return SkillRole.Dps;
			}
			if (IsBuff || IsSpawn)
			{
				return SkillRole.Support;
			}
			return SkillRole.Dps;
		}
	}
}
