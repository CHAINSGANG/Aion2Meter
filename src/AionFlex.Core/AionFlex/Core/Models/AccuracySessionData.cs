using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class AccuracySessionData
{
	[JsonPropertyName("landedHits")]
	public int LandedHits { get; set; }

	[JsonPropertyName("totalDamageGiven")]
	public long TotalDamageGiven { get; set; }

	[JsonPropertyName("blockTimes")]
	public int BlockTimes { get; set; }

	[JsonPropertyName("blockRate")]
	public double BlockRate { get; set; }

	[JsonPropertyName("qualifiedHits")]
	public int QualifiedHits { get; set; }

	[JsonPropertyName("backAttackTimes")]
	public int BackAttackTimes { get; set; }

	[JsonPropertyName("backAttackRate")]
	public double BackAttackRate { get; set; }

	[JsonPropertyName("doubleTimes")]
	public int DoubleTimes { get; set; }

	[JsonPropertyName("doubleRate")]
	public double DoubleRate { get; set; }

	[JsonPropertyName("perfectTimes")]
	public int PerfectTimes { get; set; }

	[JsonPropertyName("perfectRate")]
	public double PerfectRate { get; set; }

	[JsonPropertyName("damageLost")]
	public long DamageLost { get; set; }

	[JsonPropertyName("perSkill")]
	public List<AccuracySkillData> PerSkill { get; set; } = new List<AccuracySkillData>();
}
