using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class DefenseSessionData
{
	[JsonPropertyName("totalHits")]
	public int TotalHits { get; set; }

	[JsonPropertyName("landedHits")]
	public int LandedHits { get; set; }

	[JsonPropertyName("endureTimes")]
	public int EndureTimes { get; set; }

	[JsonPropertyName("blockTimes")]
	public int BlockTimes { get; set; }

	[JsonPropertyName("parryTimes")]
	public int ParryTimes { get; set; }

	[JsonPropertyName("evadeTimes")]
	public int EvadeTimes { get; set; }

	[JsonPropertyName("resistTimes")]
	public int ResistTimes { get; set; }

	[JsonPropertyName("regenerationTimes")]
	public int RegenerationTimes { get; set; }

	[JsonPropertyName("backAttackTimes")]
	public int BackAttackTimes { get; set; }

	[JsonPropertyName("totalDamageTaken")]
	public long TotalDamageTaken { get; set; }

	[JsonPropertyName("highestSingleHit")]
	public int HighestSingleHit { get; set; }

	[JsonPropertyName("damagePrevented")]
	public long DamagePrevented { get; set; }

	[JsonPropertyName("hitsPlain")]
	public int HitsPlain { get; set; }

	[JsonPropertyName("damagePlain")]
	public long DamagePlain { get; set; }

	[JsonPropertyName("hitsBlockOnly")]
	public int HitsBlockOnly { get; set; }

	[JsonPropertyName("damageBlockOnly")]
	public long DamageBlockOnly { get; set; }

	[JsonPropertyName("hitsEndureOnly")]
	public int HitsEndureOnly { get; set; }

	[JsonPropertyName("damageEndureOnly")]
	public long DamageEndureOnly { get; set; }

	[JsonPropertyName("hitsBlockEndure")]
	public int HitsBlockEndure { get; set; }

	[JsonPropertyName("damageBlockEndure")]
	public long DamageBlockEndure { get; set; }

	[JsonPropertyName("avoidanceRate")]
	public double AvoidanceRate { get; set; }

	[JsonPropertyName("mitigationRate")]
	public double MitigationRate { get; set; }

	[JsonPropertyName("avgDamagePerHit")]
	public double AvgDamagePerHit { get; set; }
}
