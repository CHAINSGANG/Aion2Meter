using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public sealed class HealingBreakdown
{
	public static readonly HealingBreakdown Empty = new HealingBreakdown(0L, 0.0, 0L, 0, Array.Empty<HealingBreakdownSkill>(), Array.Empty<HealingBreakdownTarget>());

	public long Total { get; }

	public double Hps { get; }

	public long ActiveTimeMs { get; }

	public int Hits { get; }

	public IReadOnlyList<HealingBreakdownSkill> Skills { get; }

	public IReadOnlyList<HealingBreakdownTarget> Targets { get; }

	private HealingBreakdown(long total, double hps, long activeTimeMs, int hits, IReadOnlyList<HealingBreakdownSkill> skills, IReadOnlyList<HealingBreakdownTarget> targets)
	{
		Total = total;
		Hps = hps;
		ActiveTimeMs = activeTimeMs;
		Hits = hits;
		Skills = skills;
		Targets = targets;
	}

	public static HealingBreakdown From(DpsSnapshot healing, int healerId, IEnumerable<DamageEvent> heals, HealingFightWindow window, Func<int, string> targetName, Func<int, long, int?>? summonOwner = null)
	{
		if (!healing.PlayerStats.TryGetValue(healerId, out PlayerStatistics value) || value.TotalDamage <= 0)
		{
			return Empty;
		}
		long total = value.TotalDamage;
		List<HealingBreakdownSkill> skills = (from s in value.GetSkillsByDamage()
			where s.TotalDamage > 0
			select new HealingBreakdownSkill(s.SkillCode, s.SkillName, s.TotalDamage, s.TotalHits, s.DpsContribution, (double)s.TotalDamage * 100.0 / (double)total)).ToList();
		Dictionary<(int, bool), (long, int, HashSet<int>)> dictionary = new Dictionary<(int, bool), (long, int, HashSet<int>)>();
		foreach (DamageEvent heal in heals)
		{
			if (heal.ActorId == healerId && heal.Damage > 0 && window.Contains(heal.Timestamp))
			{
				int? num = summonOwner?.Invoke(heal.TargetId, heal.Timestamp);
				(int, bool) valueTuple;
				if (num.HasValue)
				{
					int valueOrDefault = num.GetValueOrDefault();
					valueTuple = (valueOrDefault, true);
				}
				else
				{
					valueTuple = (heal.TargetId, false);
				}
				(int, bool) key = valueTuple;
				(long, int, HashSet<int>) valueOrDefault2 = dictionary.GetValueOrDefault(key);
				HashSet<int> hashSet = valueOrDefault2.Item3;
				if (key.Item2)
				{
					(hashSet ?? (hashSet = new HashSet<int>())).Add(heal.TargetId);
				}
				dictionary[key] = (valueOrDefault2.Item1 + heal.Damage, valueOrDefault2.Item2 + 1, hashSet);
			}
		}
		List<HealingBreakdownTarget> targets = dictionary.OrderByDescending((KeyValuePair<(int Id, bool Summons), (long Sum, int Hits, HashSet<int> Members)> t) => t.Value.Sum).ThenBy((KeyValuePair<(int Id, bool Summons), (long Sum, int Hits, HashSet<int> Members)> t) => t.Key.Summons).ThenBy((KeyValuePair<(int Id, bool Summons), (long Sum, int Hits, HashSet<int> Members)> t) => t.Key.Id)
			.Select((KeyValuePair<(int Id, bool Summons), (long Sum, int Hits, HashSet<int> Members)> t) => (!t.Key.Summons) ? new HealingBreakdownTarget(t.Key.Id, targetName(t.Key.Id), t.Value.Sum, t.Value.Hits, (double)t.Value.Sum * 100.0 / (double)total) : new HealingBreakdownTarget(t.Key.Id, SummonsName(t.Key.Id, t.Value.Members.Count, targetName), t.Value.Sum, t.Value.Hits, (double)t.Value.Sum * 100.0 / (double)total, t.Value.Members.Count))
			.ToList();
		return new HealingBreakdown(total, value.Dps, value.ActiveTimeMs, value.TotalHits, skills, targets);
	}

	public static string SummonsName(int ownerId, int count, Func<int, string> targetName)
	{
		if (ownerId > 0)
		{
			return $"{targetName(ownerId)}'s summons ({count})";
		}
		return $"Summons ({count})";
	}
}
