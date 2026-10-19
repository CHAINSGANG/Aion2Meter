using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Combat;

namespace AionFlex.Core.Models;

public sealed class DpsSnapshot
{
	public Dictionary<int, PlayerStatistics> PlayerStats { get; init; } = new Dictionary<int, PlayerStatistics>();

	public string TargetName { get; set; } = string.Empty;

	public TargetSelectionMode TargetMode { get; set; }

	public long BattleTimeMs { get; set; }

	public long ContributionDenominator { get; set; }

	public ContributionBasis ContributionBasis { get; set; }

	public bool ContributionLineApplies { get; set; }

	public ContributionDoubt ContributionDoubt { get; set; }

	public bool ContributionReliable => ContributionDoubt == ContributionDoubt.None;

	public long ContributionMeasuredDamage { get; set; }

	public int ContributionTargetId { get; set; }

	public HpProgress? ContributionProgress { get; set; }

	public double MinDps { get; set; }

	public double MaxDps { get; set; }

	public double AvgDps { get; set; }

	public long TotalDamage => PlayerStats.Values.Sum((PlayerStatistics p) => p.TotalDamage);

	public double TotalDps
	{
		get
		{
			if (BattleTimeMs <= 0)
			{
				return 0.0;
			}
			return (double)TotalDamage / ((double)BattleTimeMs / 1000.0);
		}
	}

	public int PlayerCount => PlayerStats.Count;

	public IEnumerable<PlayerStatistics> GetPlayersByDamage()
	{
		return PlayerStats.Values.OrderByDescending((PlayerStatistics p) => p.TotalDamage);
	}

	public IEnumerable<PlayerStatistics> GetPlayersByDps()
	{
		return PlayerStats.Values.OrderByDescending((PlayerStatistics p) => p.Dps);
	}
}
