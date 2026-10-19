using System;

namespace AionFlex.Core.Models;

public sealed class DefenseCounters
{
	public long FirstTouchedMs { get; set; } = long.MaxValue;

	public long LastTouchedMs { get; set; } = long.MinValue;

	public int LandedHits { get; set; }

	public int BlockTimes { get; set; }

	public int ParryTimes { get; set; }

	public int EndureTimes { get; set; }

	public int CritTimes { get; set; }

	public int QualityHits { get; set; }

	public int BackHitTimes { get; set; }

	public int FrontHitTimes { get; set; }

	public int DoubleHitTimes { get; set; }

	public int PerfectHitTimes { get; set; }

	public int ChainHits { get; set; }

	public int AdditionalHitTimes { get; set; }

	public int MultiHitMeasuredHits { get; set; }

	public int MultiHitTimes { get; set; }

	public int TargetAvoidedTimes { get; set; }

	public int TargetParriedTimes { get; set; }

	public long TargetParriedDamage { get; set; }

	public int EvadeTimes { get; set; }

	public int RegenerationTimes { get; set; }

	public int BackAttackTimes { get; set; }

	public int ResistTimes { get; set; }

	public long TotalDamageTaken { get; set; }

	public int HighestSingleHit { get; set; }

	public long DamagePlain { get; set; }

	public int HitsPlain { get; set; }

	public long DamageBlockOnly { get; set; }

	public int HitsBlockOnly { get; set; }

	public long DamageEndureOnly { get; set; }

	public int HitsEndureOnly { get; set; }

	public long DamageBlockEndure { get; set; }

	public int HitsBlockEndure { get; set; }

	public long DamagePrevented { get; set; }

	public int DotHits { get; set; }

	public long DotDamage { get; set; }

	public int TotalHits => LandedHits + EvadeTimes + ResistTimes;

	public int TotalAttempts => LandedHits;

	public double LandRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return 100.0;
		}
	}

	public double BlockRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)BlockTimes / (double)LandedHits * 100.0;
		}
	}

	public double ParryRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)ParryTimes / (double)LandedHits * 100.0;
		}
	}

	public double EndureRate
	{
		get
		{
			if (FlagMeasuredHits <= 0)
			{
				return 0.0;
			}
			return (double)EndureTimes / (double)FlagMeasuredHits * 100.0;
		}
	}

	public double EvadeRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)EvadeTimes / (double)LandedHits * 100.0;
		}
	}

	public double RegenerationRate
	{
		get
		{
			if (FlagMeasuredHits <= 0)
			{
				return 0.0;
			}
			return (double)RegenerationTimes / (double)FlagMeasuredHits * 100.0;
		}
	}

	public double BackAttackRate
	{
		get
		{
			if (FlagMeasuredHits <= 0)
			{
				return 0.0;
			}
			return (double)BackAttackTimes / (double)FlagMeasuredHits * 100.0;
		}
	}

	public double ResistRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)ResistTimes / (double)LandedHits * 100.0;
		}
	}

	public double CritRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)CritTimes / (double)LandedHits * 100.0;
		}
	}

	public bool HasHitQualityData => QualityHits > 0;

	public int FlagMeasuredHits { get; set; }

	public bool HasSpecialFlagData => FlagMeasuredHits > 0;

	public double BackHitRate
	{
		get
		{
			if (QualityHits <= 0)
			{
				return 0.0;
			}
			return (double)BackHitTimes / (double)QualityHits * 100.0;
		}
	}

	public double FrontHitRate
	{
		get
		{
			if (QualityHits <= 0)
			{
				return 0.0;
			}
			return (double)FrontHitTimes / (double)QualityHits * 100.0;
		}
	}

	public double DoubleHitRate
	{
		get
		{
			if (QualityHits <= 0)
			{
				return 0.0;
			}
			return (double)DoubleHitTimes / (double)QualityHits * 100.0;
		}
	}

	public double PerfectHitRate
	{
		get
		{
			if (QualityHits <= 0)
			{
				return 0.0;
			}
			return (double)PerfectHitTimes / (double)QualityHits * 100.0;
		}
	}

	public bool HasChainData => ChainHits > 0;

	public double AdditionalHitRate
	{
		get
		{
			if (ChainHits <= 0)
			{
				return 0.0;
			}
			return (double)AdditionalHitTimes / (double)ChainHits * 100.0;
		}
	}

	public bool HasMultiHitData => MultiHitMeasuredHits > 0;

	public double MultiHitRate
	{
		get
		{
			if (MultiHitMeasuredHits <= 0)
			{
				return 0.0;
			}
			return (double)MultiHitTimes / (double)MultiHitMeasuredHits * 100.0;
		}
	}

	public int OutgoingAttempts => LandedHits + TargetAvoidedTimes;

	public double TargetAvoidRate
	{
		get
		{
			if (OutgoingAttempts <= 0)
			{
				return 0.0;
			}
			return (double)TargetAvoidedTimes / (double)OutgoingAttempts * 100.0;
		}
	}

	public double OutgoingLandRate
	{
		get
		{
			if (OutgoingAttempts <= 0)
			{
				return 0.0;
			}
			return (double)LandedHits / (double)OutgoingAttempts * 100.0;
		}
	}

	public double TargetParryRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)TargetParriedTimes / (double)LandedHits * 100.0;
		}
	}

	public double AvoidanceRate
	{
		get
		{
			if (TotalHits <= 0)
			{
				return 0.0;
			}
			return (double)(EvadeTimes + ResistTimes) / (double)TotalHits * 100.0;
		}
	}

	public double MitigationRate
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			if (HitsPlain == 0 && HitsBlockOnly + HitsEndureOnly + HitsBlockEndure == 0)
			{
				return Math.Max(BlockRate, Math.Max(EndureRate, ParryRate));
			}
			return (double)(LandedHits - HitsPlain) / (double)LandedHits * 100.0;
		}
	}

	public double AvgDamagePerHit
	{
		get
		{
			if (LandedHits <= 0)
			{
				return 0.0;
			}
			return (double)TotalDamageTaken / (double)LandedHits;
		}
	}

	public void Touch(long timestampMs)
	{
		if (timestampMs < FirstTouchedMs)
		{
			FirstTouchedMs = timestampMs;
		}
		if (timestampMs > LastTouchedMs)
		{
			LastTouchedMs = timestampMs;
		}
	}

	public void MergeFrom(DefenseCounters other)
	{
		LandedHits += other.LandedHits;
		FlagMeasuredHits += other.FlagMeasuredHits;
		BlockTimes += other.BlockTimes;
		ParryTimes += other.ParryTimes;
		EndureTimes += other.EndureTimes;
		EvadeTimes += other.EvadeTimes;
		RegenerationTimes += other.RegenerationTimes;
		BackAttackTimes += other.BackAttackTimes;
		ResistTimes += other.ResistTimes;
		CritTimes += other.CritTimes;
		TargetParriedTimes += other.TargetParriedTimes;
		TargetParriedDamage += other.TargetParriedDamage;
		TotalDamageTaken += other.TotalDamageTaken;
		HighestSingleHit = Math.Max(HighestSingleHit, other.HighestSingleHit);
		DamagePlain += other.DamagePlain;
		HitsPlain += other.HitsPlain;
		DamageBlockOnly += other.DamageBlockOnly;
		HitsBlockOnly += other.HitsBlockOnly;
		DamageEndureOnly += other.DamageEndureOnly;
		HitsEndureOnly += other.HitsEndureOnly;
		DamageBlockEndure += other.DamageBlockEndure;
		HitsBlockEndure += other.HitsBlockEndure;
		DamagePrevented += other.DamagePrevented;
	}

	public void MergeFromOutgoing(DefenseCounters other)
	{
		LandedHits += other.LandedHits;
		CritTimes += other.CritTimes;
		QualityHits += other.QualityHits;
		BackHitTimes += other.BackHitTimes;
		FrontHitTimes += other.FrontHitTimes;
		DoubleHitTimes += other.DoubleHitTimes;
		PerfectHitTimes += other.PerfectHitTimes;
		ChainHits += other.ChainHits;
		AdditionalHitTimes += other.AdditionalHitTimes;
		MultiHitMeasuredHits += other.MultiHitMeasuredHits;
		MultiHitTimes += other.MultiHitTimes;
		TargetAvoidedTimes += other.TargetAvoidedTimes;
		TargetParriedTimes += other.TargetParriedTimes;
		TargetParriedDamage += other.TargetParriedDamage;
		TotalDamageTaken += other.TotalDamageTaken;
		HighestSingleHit = Math.Max(HighestSingleHit, other.HighestSingleHit);
		HitsPlain += other.LandedHits;
		DamagePlain += other.TotalDamageTaken;
	}
}
