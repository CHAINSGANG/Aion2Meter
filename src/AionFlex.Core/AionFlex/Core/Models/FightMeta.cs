using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class FightMeta
{
	[JsonPropertyName("killSignal")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? KillSignal { get; set; }

	[JsonPropertyName("bossMaxHp")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? BossMaxHp { get; set; }

	[JsonPropertyName("bossHpTrusted")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? BossHpTrusted { get; set; }

	[JsonPropertyName("contributionBasis")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ContributionBasis { get; set; }

	[JsonPropertyName("contributionDenominator")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? ContributionDenominator { get; set; }

	[JsonPropertyName("battleTimeClampedFromMs")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? BattleTimeClampedFromMs { get; set; }

	[JsonPropertyName("wipeCount")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? WipeCount { get; set; }

	[JsonPropertyName("attemptIndex")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? AttemptIndex { get; set; }

	[JsonPropertyName("wipeRuleVersion")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? WipeRuleVersion { get; set; }

	[JsonPropertyName("targetMode")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? TargetMode { get; set; }

	[JsonPropertyName("entityLifeGeneration")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? EntityLifeGeneration { get; set; }
}
