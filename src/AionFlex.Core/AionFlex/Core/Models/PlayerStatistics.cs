using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Models;

public sealed class PlayerStatistics
{
	public const long MinActiveTimeMs = 1000L;

	public int PlayerId { get; init; }

	public string Nickname { get; set; } = string.Empty;

	public JobClass Job { get; set; }

	public long TotalDamage { get; set; }

	public long DpsDamage { get; set; }

	public long SupportDamage { get; set; }

	public long HealAmount { get; set; }

	public double Dps { get; set; }

	public double SupportDps { get; set; }

	public double DamageContribution { get; set; }

	public double PartyDamageShare { get; set; }

	public int GearScore { get; set; }

	public int CombatPower { get; set; }

	public int Level { get; set; }

	public long FirstHitTime { get; set; } = long.MaxValue;

	public long LastHitTime { get; set; } = long.MinValue;

	public long ActiveTimeMs { get; private set; }

	public int TotalHits { get; set; }

	public int CritHits { get; set; }

	public int QualifiedHits { get; set; }

	public int BackHits { get; set; }

	public int FrontHits { get; set; }

	public int DoubleHits { get; set; }

	public int PerfectHits { get; set; }

	public int ModifiedHits { get; set; }

	public int BlockHits { get; set; }

	public int ParryHits { get; set; }

	public double CritRate
	{
		get
		{
			if (TotalHits <= 0)
			{
				return 0.0;
			}
			return (double)CritHits / (double)TotalHits * 100.0;
		}
	}

	public bool HasHitQualityData => QualifiedHits > 0;

	public double BackRate
	{
		get
		{
			if (QualifiedHits <= 0)
			{
				return 0.0;
			}
			return (double)BackHits / (double)QualifiedHits * 100.0;
		}
	}

	public double FrontRate
	{
		get
		{
			if (QualifiedHits <= 0)
			{
				return 0.0;
			}
			return (double)FrontHits / (double)QualifiedHits * 100.0;
		}
	}

	public double DoubleRate
	{
		get
		{
			if (QualifiedHits <= 0)
			{
				return 0.0;
			}
			return (double)DoubleHits / (double)QualifiedHits * 100.0;
		}
	}

	public double PerfectRate
	{
		get
		{
			if (QualifiedHits <= 0)
			{
				return 0.0;
			}
			return (double)PerfectHits / (double)QualifiedHits * 100.0;
		}
	}

	public double BlockRate
	{
		get
		{
			if (TotalHits <= 0)
			{
				return 0.0;
			}
			return (double)BlockHits / (double)TotalHits * 100.0;
		}
	}

	public double ParryRate
	{
		get
		{
			if (TotalHits <= 0)
			{
				return 0.0;
			}
			return (double)ParryHits / (double)TotalHits * 100.0;
		}
	}

	public double ModifiedRate
	{
		get
		{
			if (QualifiedHits <= 0)
			{
				return 0.0;
			}
			return (double)ModifiedHits / (double)QualifiedHits * 100.0;
		}
	}

	public double NormalRate
	{
		get
		{
			if (QualifiedHits <= 0)
			{
				return 0.0;
			}
			return (double)(QualifiedHits - ModifiedHits) / (double)QualifiedHits * 100.0;
		}
	}

	public Dictionary<int, SkillStatistics> SkillBreakdown { get; } = new Dictionary<int, SkillStatistics>();

	public PlayerStatistics(int playerId, string nickname)
	{
		PlayerId = playerId;
		Nickname = nickname;
	}

	public void ProcessDamageEvent(DamageEvent damageEvent, string skillName, SkillRole role = SkillRole.Dps, int? inferredSkillCode = null)
	{
		switch (role)
		{
		case SkillRole.Support:
			TotalDamage += damageEvent.TotalDamage;
			SupportDamage += damageEvent.TotalDamage;
			break;
		case SkillRole.Heal:
			HealAmount += damageEvent.TotalDamage;
			break;
		default:
			TotalDamage += damageEvent.TotalDamage;
			DpsDamage += damageEvent.TotalDamage;
			break;
		}
		if (role != SkillRole.Heal)
		{
			if (damageEvent.Timestamp < FirstHitTime)
			{
				FirstHitTime = damageEvent.Timestamp;
			}
			if (damageEvent.Timestamp > LastHitTime)
			{
				LastHitTime = damageEvent.Timestamp;
			}
		}
		if (!damageEvent.IsDoT)
		{
			TotalHits++;
			if (damageEvent.IsCritical)
			{
				CritHits++;
			}
			HitQuality quality = damageEvent.Quality;
			if (quality.IsQualified)
			{
				QualifiedHits++;
				if (quality.IsBack)
				{
					BackHits++;
				}
				else if (quality.IsFront)
				{
					FrontHits++;
				}
				if (quality.IsDouble)
				{
					DoubleHits++;
				}
				if (quality.IsPerfect)
				{
					PerfectHits++;
				}
				if (quality.IsDouble || quality.IsPerfect)
				{
					ModifiedHits++;
				}
			}
		}
		int num = inferredSkillCode ?? NormalizeSkillCodeForGrouping(damageEvent.SkillCode);
		if (!SkillBreakdown.ContainsKey(num))
		{
			SkillBreakdown[num] = new SkillStatistics(num, skillName)
			{
				Role = role
			};
		}
		SkillBreakdown[num].ProcessDamageEvent(damageEvent);
	}

	public long ResolveActiveTimeMs(long fightEndMs, long fightDurationMs)
	{
		if (FirstHitTime == long.MaxValue || fightDurationMs <= 0)
		{
			return 0L;
		}
		return Math.Max(Math.Min(fightEndMs - FirstHitTime, fightDurationMs), 1000L);
	}

	public double WholeFightDps(long fightDurationMs)
	{
		if (fightDurationMs <= 0)
		{
			return 0.0;
		}
		return (double)DpsDamage / ((double)fightDurationMs / 1000.0);
	}

	public void CalculateDps(long activeTimeMs)
	{
		if (activeTimeMs > 0)
		{
			ActiveTimeMs = activeTimeMs;
			double num = (double)activeTimeMs / 1000.0;
			Dps = (double)DpsDamage / num;
			SupportDps = (double)SupportDamage / num;
		}
		else
		{
			ActiveTimeMs = 0L;
			Dps = 0.0;
			SupportDps = 0.0;
		}
	}

	public void CalculateContribution(long contributionDenominator, long totalDamageAllPlayers, long? contributionNumerator = null)
	{
		DamageContribution = ((contributionDenominator > 0) ? ((double)(contributionNumerator ?? TotalDamage) / (double)contributionDenominator * 100.0) : 0.0);
		PartyDamageShare = ((totalDamageAllPlayers > 0) ? ((double)TotalDamage / (double)totalDamageAllPlayers * 100.0) : 0.0);
	}

	public void CalculateSkillContributions(long activeTimeMs)
	{
		double num = ((activeTimeMs > 0) ? ((double)activeTimeMs / 1000.0) : 0.0);
		foreach (SkillStatistics value in SkillBreakdown.Values)
		{
			value.DpsContribution = ((num > 0.0) ? ((double)value.TotalDamage / num) : 0.0);
			value.DamagePercentage = ((TotalDamage > 0) ? ((double)value.TotalDamage / (double)TotalDamage * 100.0) : 0.0);
		}
	}

	public IEnumerable<SkillStatistics> GetSkillsByDamage()
	{
		return from s in SkillBreakdown.Values
			where s.Role != SkillRole.Heal
			orderby s.TotalDamage descending
			select s;
	}

	public void ConsolidateSkillsByName()
	{
		foreach (IGrouping<string, KeyValuePair<int, SkillStatistics>> item in (from kvp in SkillBreakdown
			group kvp by kvp.Value.SkillName into g
			where g.Count() > 1
			select g).ToList())
		{
			KeyValuePair<int, SkillStatistics> keyValuePair = item.OrderByDescending((KeyValuePair<int, SkillStatistics> kvp) => kvp.Value.TotalDamage).First();
			foreach (KeyValuePair<int, SkillStatistics> item2 in item)
			{
				if (item2.Key != keyValuePair.Key)
				{
					keyValuePair.Value.MergeFrom(item2.Value);
					SkillBreakdown.Remove(item2.Key);
				}
			}
		}
	}

	private static int NormalizeSkillCodeForGrouping(int skillCode)
	{
		return SkillCodeNormalizer.Normalize(skillCode);
	}
}
