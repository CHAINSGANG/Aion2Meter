using System.Collections.Generic;

namespace AionFlex.Core.Combat;

public sealed record EliminationPick(EliminationOutcome Outcome, int NpcId, string? RunTier, IReadOnlyList<int> Proof, IReadOnlyList<int> Candidates)
{
	public string Describe(string bossName)
	{
		return Outcome switch
		{
			EliminationOutcome.Picked => $"'{bossName}' at tier '{RunTier}' is npc {NpcId} (tier proved by the spawns of {string.Join(", ", Proof)})", 
			EliminationOutcome.RunTierUnproven => "no other boss of this dungeon spawned in this run, so nothing proves which '" + bossName + "' variant this is", 
			EliminationOutcome.RunTierMixed => "the bosses spawned in this run carry different tiers (" + string.Join(", ", Proof) + ")", 
			EliminationOutcome.NoCandidate => $"no '{bossName}' npc has the run's tier '{RunTier}'", 
			EliminationOutcome.Ambiguous => $"{Candidates.Count} '{bossName}' npcs have the run's tier '{RunTier}' ({string.Join(", ", Candidates)})", 
			_ => Outcome.ToString(), 
		};
	}
}
