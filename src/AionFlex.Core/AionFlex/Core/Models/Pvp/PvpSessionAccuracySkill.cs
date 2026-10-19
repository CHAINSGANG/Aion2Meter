using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionAccuracySkill
{
	[JsonPropertyName("skillCode")]
	public int SkillCode { get; set; }

	[JsonPropertyName("skillName")]
	public string SkillName { get; set; } = "";

	[JsonPropertyName("landedHits")]
	public int LandedHits { get; set; }

	[JsonPropertyName("totalDamage")]
	public long TotalDamage { get; set; }

	[JsonPropertyName("damagePercent")]
	public double DamagePercent { get; set; }

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

	[JsonPropertyName("hitsPlain")]
	public int HitsPlain { get; set; }

	[JsonPropertyName("damagePlain")]
	public long DamagePlain { get; set; }

	[JsonPropertyName("hitsBlockOnly")]
	public int HitsBlockOnly { get; set; }

	[JsonPropertyName("damageBlockOnly")]
	public long DamageBlockOnly { get; set; }
}
