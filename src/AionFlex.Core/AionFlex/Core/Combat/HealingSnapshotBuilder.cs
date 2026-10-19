using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class HealingSnapshotBuilder
{
	public const string SnapshotName = "Healing (Beta)";

	public static DpsSnapshot Build(IEnumerable<DamageEvent> heals, Func<int, string> healerName, Func<int, string> skillName, HealingFightWindow? window = null)
	{
		HealingFightWindow healingFightWindow = window ?? HealingFightWindow.Unbounded;
		DpsSnapshot dpsSnapshot = new DpsSnapshot
		{
			TargetName = "Healing (Beta)",
			TargetMode = TargetSelectionMode.AllTargets
		};
		long num = long.MaxValue;
		long num2 = long.MinValue;
		long num3 = 0L;
		Dictionary<int, Dictionary<JobClass, int>> dictionary = new Dictionary<int, Dictionary<JobClass, int>>();
		foreach (DamageEvent heal in heals)
		{
			if (heal.Damage <= 0 || !healingFightWindow.Contains(heal.Timestamp))
			{
				continue;
			}
			if (heal.Timestamp < num)
			{
				num = heal.Timestamp;
			}
			if (heal.Timestamp > num2)
			{
				num2 = heal.Timestamp;
			}
			num3 += heal.Damage;
			if (!dpsSnapshot.PlayerStats.TryGetValue(heal.ActorId, out PlayerStatistics value))
			{
				value = (dpsSnapshot.PlayerStats[heal.ActorId] = new PlayerStatistics(heal.ActorId, healerName(heal.ActorId)));
			}
			value.ProcessDamageEvent(heal, skillName(heal.SkillCode), SkillRole.Dps, heal.SkillCode);
			JobClass jobClass = JobClassExtensions.FromSkillCode(heal.SkillCode);
			if (jobClass != JobClass.Unknown)
			{
				if (!dictionary.TryGetValue(heal.ActorId, out var value2))
				{
					value2 = (dictionary[heal.ActorId] = new Dictionary<JobClass, int>());
				}
				value2[jobClass] = value2.GetValueOrDefault(jobClass) + 1;
			}
		}
		if (num3 == 0L)
		{
			return dpsSnapshot;
		}
		long fightDurationMs = (dpsSnapshot.BattleTimeMs = num2 - num);
		foreach (var (key, playerStatistics3) in dpsSnapshot.PlayerStats)
		{
			long activeTimeMs = playerStatistics3.ResolveActiveTimeMs(num2, fightDurationMs);
			playerStatistics3.CalculateDps(activeTimeMs);
			playerStatistics3.CalculateContribution(num3, num3);
			playerStatistics3.CalculateSkillContributions(activeTimeMs);
			playerStatistics3.Job = (dictionary.TryGetValue(key, out var value3) ? value3.OrderByDescending((KeyValuePair<JobClass, int> v) => v.Value).First().Key : JobClass.Unknown);
		}
		dpsSnapshot.ContributionDenominator = num3;
		dpsSnapshot.ContributionBasis = ContributionBasis.PartyDamage;
		return dpsSnapshot;
	}
}
