namespace AionFlex.Core.Combat;

public static class PartyMemberEvidencePolicy
{
	public const long PlayerTierHpCeiling = 100000L;

	public static PlayerEvidence Evaluate(bool isLocalPlayer, bool isNamedPlayer, bool healedByPlayer)
	{
		if (!isLocalPlayer)
		{
			if (!isNamedPlayer)
			{
				if (!healedByPlayer)
				{
					return PlayerEvidence.None;
				}
				return PlayerEvidence.HealedByPlayer;
			}
			return PlayerEvidence.NamedPlayer;
		}
		return PlayerEvidence.LocalPlayer;
	}

	public static bool IsProvenPlayer(bool isLocalPlayer, bool isNamedPlayer, bool healedByPlayer)
	{
		return Evaluate(isLocalPlayer, isNamedPlayer, healedByPlayer) != PlayerEvidence.None;
	}

	public static string Describe(PlayerEvidence evidence)
	{
		return evidence switch
		{
			PlayerEvidence.LocalPlayer => "local player", 
			PlayerEvidence.NamedPlayer => "named player", 
			PlayerEvidence.HealedByPlayer => "healed by a player", 
			_ => "no player evidence", 
		};
	}

	public static bool CountsAsAbsorbedDamage(bool isHeal, int actorId, int targetId)
	{
		if (!isHeal)
		{
			return actorId != targetId;
		}
		return false;
	}

	public static bool HasPlayerTierHpBar(long knownMaxHp)
	{
		if (knownMaxHp > 0)
		{
			return knownMaxHp < 100000;
		}
		return false;
	}
}
