using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionSkill
{
	[JsonPropertyName("skillCode")]
	public int SkillCode { get; set; }

	[JsonPropertyName("skillName")]
	public string SkillName { get; set; } = "";

	[JsonPropertyName("role")]
	public string Role { get; set; } = "Dps";

	[JsonPropertyName("hits")]
	public int Hits { get; set; }

	[JsonPropertyName("dotHits")]
	public int DotHits { get; set; }

	[JsonPropertyName("critHits")]
	public int CritHits { get; set; }

	[JsonPropertyName("backHits")]
	public int BackHits { get; set; }

	[JsonPropertyName("doubleHits")]
	public int DoubleHits { get; set; }

	[JsonPropertyName("perfectHits")]
	public int PerfectHits { get; set; }

	[JsonPropertyName("damage")]
	public long Damage { get; set; }

	[JsonPropertyName("dotDamage")]
	public long DotDamage { get; set; }

	[JsonPropertyName("totalDamage")]
	public long TotalDamage { get; set; }

	[JsonPropertyName("minDamage")]
	public long MinDamage { get; set; }

	[JsonPropertyName("maxDamage")]
	public long MaxDamage { get; set; }

	[JsonPropertyName("critRate")]
	public double CritRate { get; set; }

	[JsonPropertyName("damagePercent")]
	public double DamagePercent { get; set; }

	[JsonPropertyName("dps")]
	public double Dps { get; set; }
}
