using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class SkillStatistics
{
	public const int MinHitsForOutlierGuard = 4;

	public const long HighestHitOutlierFactor = 8L;

	public int SkillCode { get; init; }

	public int RawSkillCode { get; set; }

	public HashSet<int> SeenRawCodes { get; } = new HashSet<int>();

	public string SkillName { get; init; } = string.Empty;

	public SkillRole Role { get; set; }

	public long DamageAmount { get; set; }

	public int Times { get; set; }

	public int CritTimes { get; set; }

	public long DotDamageAmount { get; set; }

	public int DotTimes { get; set; }

	public int MultiHitCount { get; set; }

	public long MultiHitDamage { get; set; }

	public int QualifiedHits { get; set; }

	public int BackTimes { get; set; }

	public int FrontTimes { get; set; }

	public int PerfectTimes { get; set; }

	public int DoubleTimes { get; set; }

	public int BlockTimes { get; set; }

	public int ParryTimes { get; set; }

	public long MinDamage { get; set; } = long.MaxValue;

	public long MaxDamage { get; set; }

	public long MinDotDamage { get; set; } = long.MaxValue;

	public long MaxDotDamage { get; set; }

	public long Max2Damage { get; set; }

	public long Max2DotDamage { get; set; }

	public double DpsContribution { get; set; }

	public double DamagePercentage { get; set; }

	public long TotalDamage => DamageAmount + DotDamageAmount;

	public int TotalHits => Times + DotTimes;

	public double CritRate
	{
		get
		{
			if (Times <= 0)
			{
				return 0.0;
			}
			return (double)CritTimes / (double)Times * 100.0;
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
			return (double)BackTimes / (double)QualifiedHits * 100.0;
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
			return (double)FrontTimes / (double)QualifiedHits * 100.0;
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
			return (double)DoubleTimes / (double)QualifiedHits * 100.0;
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
			return (double)PerfectTimes / (double)QualifiedHits * 100.0;
		}
	}

	public double AverageDamage
	{
		get
		{
			if (Times <= 0)
			{
				return 0.0;
			}
			return (double)DamageAmount / (double)Times;
		}
	}

	public double AverageDotDamage
	{
		get
		{
			if (DotTimes <= 0)
			{
				return 0.0;
			}
			return (double)DotDamageAmount / (double)DotTimes;
		}
	}

	public long OverallMinDamage
	{
		get
		{
			if (TotalHits != 0)
			{
				if (Times <= 0 || DotTimes <= 0)
				{
					if (Times <= 0)
					{
						return MinDotDamage;
					}
					return MinDamage;
				}
				return Math.Min(MinDamage, MinDotDamage);
			}
			return 0L;
		}
	}

	public long OverallMaxDamage => Math.Max(MaxDamage, MaxDotDamage);

	public long GuardedMaxDamage
	{
		get
		{
			if (Times < 4 || Max2Damage <= 0 || MaxDamage <= 8 * Max2Damage)
			{
				return MaxDamage;
			}
			return Max2Damage;
		}
	}

	public long GuardedMaxDotDamage
	{
		get
		{
			if (DotTimes < 4 || Max2DotDamage <= 0 || MaxDotDamage <= 8 * Max2DotDamage)
			{
				return MaxDotDamage;
			}
			return Max2DotDamage;
		}
	}

	public long GuardedOverallMax => Math.Max(GuardedMaxDamage, GuardedMaxDotDamage);

	public bool HasMultiHits => MultiHitCount > 0;

	public double AverageMultiHitDamage
	{
		get
		{
			if (MultiHitCount <= 0)
			{
				return 0.0;
			}
			return (double)MultiHitDamage / (double)MultiHitCount;
		}
	}

	public SkillStatistics(int skillCode, string skillName)
	{
		SkillCode = skillCode;
		SkillName = skillName;
	}

	public static long GuardedHighestHit(IEnumerable<SkillStatistics> skills)
	{
		long num = 0L;
		long num2 = 0L;
		int num3 = 0;
		foreach (SkillStatistics skill in skills)
		{
			long guardedOverallMax = skill.GuardedOverallMax;
			if (guardedOverallMax > num)
			{
				num2 = num;
				num = guardedOverallMax;
				num3 = skill.TotalHits;
			}
			else if (guardedOverallMax > num2)
			{
				num2 = guardedOverallMax;
			}
		}
		if (num3 < 4 && num2 > 0 && num > 8 * num2)
		{
			return num2;
		}
		return num;
	}

	public void MergeFrom(SkillStatistics other)
	{
		DamageAmount += other.DamageAmount;
		Times += other.Times;
		CritTimes += other.CritTimes;
		DotDamageAmount += other.DotDamageAmount;
		DotTimes += other.DotTimes;
		MultiHitCount += other.MultiHitCount;
		MultiHitDamage += other.MultiHitDamage;
		QualifiedHits += other.QualifiedHits;
		BackTimes += other.BackTimes;
		FrontTimes += other.FrontTimes;
		PerfectTimes += other.PerfectTimes;
		DoubleTimes += other.DoubleTimes;
		BlockTimes += other.BlockTimes;
		ParryTimes += other.ParryTimes;
		if (other.MinDamage < MinDamage)
		{
			MinDamage = other.MinDamage;
		}
		if (other.MinDotDamage < MinDotDamage)
		{
			MinDotDamage = other.MinDotDamage;
		}
		(MaxDamage, Max2Damage) = MergeTop2(MaxDamage, Max2Damage, other.MaxDamage, other.Max2Damage);
		(MaxDotDamage, Max2DotDamage) = MergeTop2(MaxDotDamage, Max2DotDamage, other.MaxDotDamage, other.Max2DotDamage);
		foreach (int seenRawCode in other.SeenRawCodes)
		{
			SeenRawCodes.Add(seenRawCode);
		}
	}

	private static (long Max1, long Max2) MergeTop2(long aMax1, long aMax2, long bMax1, long bMax2)
	{
		long max = aMax1;
		long max2 = aMax2;
		Offer(ref max, ref max2, bMax1);
		Offer(ref max, ref max2, bMax2);
		return (Max1: max, Max2: max2);
		static void Offer(ref long reference, ref long reference2, long v)
		{
			if (v > reference)
			{
				reference2 = reference;
				reference = v;
			}
			else if (v > reference2)
			{
				reference2 = v;
			}
		}
	}

	public void ProcessDamageEvent(DamageEvent damageEvent)
	{
		if (damageEvent.RawSkillCode != 0)
		{
			RawSkillCode = damageEvent.RawSkillCode;
			SeenRawCodes.Add(damageEvent.RawSkillCode);
		}
		if (damageEvent.IsDoT)
		{
			DotTimes++;
			DotDamageAmount += damageEvent.Damage;
			int damage = damageEvent.Damage;
			MinDotDamage = Math.Min(MinDotDamage, damage);
			if (damage > MaxDotDamage)
			{
				Max2DotDamage = MaxDotDamage;
				MaxDotDamage = damage;
			}
			else if (damage > Max2DotDamage)
			{
				Max2DotDamage = damage;
			}
			return;
		}
		Times++;
		DamageAmount += damageEvent.TotalDamage;
		int totalDamage = damageEvent.TotalDamage;
		MinDamage = Math.Min(MinDamage, totalDamage);
		if (totalDamage > MaxDamage)
		{
			Max2Damage = MaxDamage;
			MaxDamage = totalDamage;
		}
		else if (totalDamage > Max2Damage)
		{
			Max2Damage = totalDamage;
		}
		if (damageEvent.IsCritical)
		{
			CritTimes++;
		}
		HitQuality quality = damageEvent.Quality;
		if (quality.IsQualified)
		{
			QualifiedHits++;
			if (quality.IsBack)
			{
				BackTimes++;
			}
			else if (quality.IsFront)
			{
				FrontTimes++;
			}
			if (quality.IsDouble)
			{
				DoubleTimes++;
			}
			if (quality.IsPerfect)
			{
				PerfectTimes++;
			}
		}
		if (damageEvent.MultiHitCount > 0)
		{
			MultiHitCount += damageEvent.MultiHitCount;
			MultiHitDamage += damageEvent.MultiHitDamage;
		}
	}
}
