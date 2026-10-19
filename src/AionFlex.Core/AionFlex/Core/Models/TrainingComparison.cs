using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class TrainingComparison
{
	public required TrainingSession Baseline { get; init; }

	public required TrainingSession Compared { get; init; }

	public required ValueDelta Dps { get; init; }

	public required ValueDelta TotalDamage { get; init; }

	public required ValueDelta BattleTimeSeconds { get; init; }

	public required ValueDelta TotalHits { get; init; }

	public required ValueDelta CritRate { get; init; }

	public required ValueDelta BackRate { get; init; }

	public required ValueDelta DoubleRate { get; init; }

	public required ValueDelta PerfectRate { get; init; }

	public required List<SkillComparisonRow> Skills { get; init; }

	public string Verdict
	{
		get
		{
			if (Dps.Baseline == 0.0 && Dps.Compared == 0.0)
			{
				return "No damage recorded";
			}
			string value = (Dps.IsImprovement ? "improved" : "decreased");
			string value2 = ((Dps.AbsoluteDelta >= 0.0) ? "+" : "");
			return $"DPS {value} by {Math.Abs(Dps.PercentDelta):F1}% ({value2}{Dps.AbsoluteDelta:N0} DPS)";
		}
	}
}
