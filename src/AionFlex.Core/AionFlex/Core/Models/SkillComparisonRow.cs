namespace AionFlex.Core.Models;

public sealed class SkillComparisonRow
{
	public int SkillCode { get; init; }

	public string SkillName { get; init; } = "";

	public bool InBaseline { get; init; }

	public bool InCompared { get; init; }

	public bool IsNew
	{
		get
		{
			if (InCompared)
			{
				return !InBaseline;
			}
			return false;
		}
	}

	public bool IsRemoved
	{
		get
		{
			if (InBaseline)
			{
				return !InCompared;
			}
			return false;
		}
	}

	public required ValueDelta Dps { get; init; }

	public required ValueDelta TotalDamage { get; init; }

	public required ValueDelta AverageDamage { get; init; }

	public required ValueDelta MinDamage { get; init; }

	public required ValueDelta MaxDamage { get; init; }

	public required ValueDelta Hits { get; init; }

	public required ValueDelta DotHits { get; init; }

	public required ValueDelta CritRate { get; init; }

	public required ValueDelta BackRate { get; init; }

	public required ValueDelta DoubleRate { get; init; }

	public required ValueDelta PerfectRate { get; init; }

	public required ValueDelta HitsPerMinute { get; init; }

	public required ValueDelta DamagePercent { get; init; }
}
