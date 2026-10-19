using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionAccuracy
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

	[JsonPropertyName("damageLost")]
	public long DamageLost { get; set; }

	[JsonPropertyName("perSkill")]
	public List<PvpSessionAccuracySkill> PerSkill { get; set; } = new List<PvpSessionAccuracySkill>();
}
