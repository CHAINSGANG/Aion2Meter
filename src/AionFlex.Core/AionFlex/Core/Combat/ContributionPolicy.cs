using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class ContributionPolicy
{
	public const double LowContributionPercent = 5.0;

	public const double LineStartRemovedFraction = 0.05;

	public const double OvercountTolerance = 0.02;

	public const double UndercountTolerance = 0.1;

	public static bool UsesPartyDamageBasis(string? dungeonCategory)
	{
		if (dungeonCategory == "hideout" || dungeonCategory == "stronghold")
		{
			return true;
		}
		return false;
	}

	public static ContributionCheck Check(long bar, HpProgress? progress, long keptDamageAsOfLowestReading)
	{
		if (bar > 0 && progress.HasValue)
		{
			HpProgress valueOrDefault = progress.GetValueOrDefault();
			long num = ((valueOrDefault.MinAliveHp > 0 && valueOrDefault.MinAliveHp < bar) ? (bar - valueOrDefault.MinAliveHp) : 0);
			bool flag = (double)num >= 0.05 * (double)bar;
			long num2 = ((flag && !valueOrDefault.ReachedZeroHp) ? num : bar);
			ContributionDoubt doubt = (((double)(keptDamageAsOfLowestReading - num) > 0.02 * (double)num2) ? ContributionDoubt.OverCount : ((flag && (double)(num - keptDamageAsOfLowestReading) > 0.1 * (double)num) ? ContributionDoubt.UnderCount : ContributionDoubt.None));
			return new ContributionCheck(flag || valueOrDefault.ReachedZeroHp, doubt);
		}
		return new ContributionCheck(LineApplies: false);
	}

	public static bool PartyShareLineApplies(HpProgress? progress)
	{
		if (progress.HasValue)
		{
			HpProgress valueOrDefault = progress.GetValueOrDefault();
			if (valueOrDefault.ReachedZeroHp)
			{
				return true;
			}
			if (valueOrDefault.ObservedMaxHp > 0 && valueOrDefault.MinAliveHp > 0)
			{
				return (double)(valueOrDefault.ObservedMaxHp - valueOrDefault.MinAliveHp) >= 0.05 * (double)valueOrDefault.ObservedMaxHp;
			}
			return false;
		}
		return false;
	}

	public static ContributionTone ToneFor(double percent, bool lineApplies, bool reliable)
	{
		if (!reliable)
		{
			return ContributionTone.Unreliable;
		}
		if (!lineApplies || !(Displayed(percent) < 5.0))
		{
			return ContributionTone.Normal;
		}
		return ContributionTone.Low;
	}

	public static double Displayed(double percent)
	{
		return Math.Round(percent, 1, MidpointRounding.AwayFromZero);
	}
}
