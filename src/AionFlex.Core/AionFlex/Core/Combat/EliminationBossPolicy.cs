using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Combat;

public static class EliminationBossPolicy
{
	public static IReadOnlyList<string> RemainingBossNames(IEnumerable<int> dungeonBossNpcIds, IEnumerable<int> identifiedNpcIds, Func<int, string?> nameOf)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (int identifiedNpcId in identifiedNpcIds)
		{
			string text = nameOf(identifiedNpcId);
			if (text != null)
			{
				hashSet.Add(text);
			}
		}
		SortedSet<string> sortedSet = new SortedSet<string>(StringComparer.Ordinal);
		foreach (int dungeonBossNpcId in dungeonBossNpcIds)
		{
			string text2 = nameOf(dungeonBossNpcId);
			if (text2 != null && !hashSet.Contains(text2))
			{
				sortedSet.Add(text2);
			}
		}
		return sortedSet.ToList();
	}

	public static EliminationPick Pick(IEnumerable<int> dungeonBossNpcIds, string remainingName, IEnumerable<int> runBossSpawnNpcIds, Func<int, string?> nameOf, Func<int, string?> tierOf)
	{
		SortedSet<int> sortedSet = new SortedSet<int>(dungeonBossNpcIds);
		SortedSet<int> sortedSet2 = new SortedSet<int>();
		SortedSet<string> sortedSet3 = new SortedSet<string>(StringComparer.Ordinal);
		foreach (int runBossSpawnNpcId in runBossSpawnNpcIds)
		{
			if (sortedSet.Contains(runBossSpawnNpcId) && nameOf(runBossSpawnNpcId) != null)
			{
				string text = tierOf(runBossSpawnNpcId);
				if (text != null)
				{
					sortedSet2.Add(runBossSpawnNpcId);
					sortedSet3.Add(text);
				}
			}
		}
		if (sortedSet3.Count == 0)
		{
			return new EliminationPick(EliminationOutcome.RunTierUnproven, 0, null, sortedSet2.ToList(), Array.Empty<int>());
		}
		if (sortedSet3.Count > 1)
		{
			return new EliminationPick(EliminationOutcome.RunTierMixed, 0, null, sortedSet2.ToList(), Array.Empty<int>());
		}
		string runTier = sortedSet3.Min;
		List<int> list = sortedSet.Where((int npcId) => nameOf(npcId) == remainingName && tierOf(npcId) == runTier).ToList();
		return list.Count switch
		{
			0 => new EliminationPick(EliminationOutcome.NoCandidate, 0, runTier, sortedSet2.ToList(), list), 
			1 => new EliminationPick(EliminationOutcome.Picked, list[0], runTier, sortedSet2.ToList(), list), 
			_ => new EliminationPick(EliminationOutcome.Ambiguous, 0, runTier, sortedSet2.ToList(), list), 
		};
	}
}
