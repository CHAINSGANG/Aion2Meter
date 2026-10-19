using System;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class PersonalBestRecord
{
	[JsonPropertyName("characterName")]
	public string CharacterName { get; init; } = "";

	[JsonPropertyName("bossName")]
	public string BossName { get; init; } = "";

	[JsonPropertyName("instanceName")]
	public string InstanceName { get; init; } = "";

	[JsonPropertyName("dps")]
	public double Dps { get; init; }

	[JsonPropertyName("totalDamage")]
	public long TotalDamage { get; init; }

	[JsonPropertyName("achievedAt")]
	public DateTime AchievedAt { get; init; }

	[JsonPropertyName("battleTime")]
	public string BattleTime { get; init; } = "";
}
