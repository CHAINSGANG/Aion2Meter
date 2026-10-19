using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class BossSession
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 2;

	[JsonPropertyName("sessionId")]
	public string SessionId { get; set; } = Guid.NewGuid().ToString();

	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	[JsonPropertyName("battleTimeClampedFromMs")]
	public long? BattleTimeClampedFromMs { get; set; }

	[JsonPropertyName("characterName")]
	public string CharacterName { get; set; } = "";

	[JsonPropertyName("serverId")]
	public int? ServerId { get; set; }

	[JsonPropertyName("serverShortCode")]
	public string? ServerShortCode { get; set; }

	[JsonPropertyName("serverName")]
	public string? ServerName { get; set; }

	[JsonPropertyName("region")]
	public string? Region { get; set; }

	[JsonPropertyName("faction")]
	public string? Faction { get; set; }

	[JsonPropertyName("characterId")]
	public uint? CharacterId { get; set; }

	[JsonPropertyName("clientVersion")]
	public string? ClientVersion { get; set; }

	[JsonPropertyName("startedAt")]
	public DateTime? StartedAt { get; set; }

	[JsonPropertyName("instanceName")]
	public string InstanceName { get; set; } = "";

	[JsonPropertyName("instanceKey")]
	public string? InstanceKey { get; set; }

	[JsonPropertyName("bossName")]
	public string BossName { get; set; } = "";

	[JsonPropertyName("bossEntityId")]
	public int BossEntityId { get; set; }

	[JsonPropertyName("bossNpcId")]
	public int? BossNpcId { get; set; }

	[JsonPropertyName("battleTimeMs")]
	public long BattleTimeMs { get; set; }

	[JsonPropertyName("totalDamage")]
	public long TotalDamage { get; set; }

	[JsonPropertyName("totalDps")]
	public double TotalDps { get; set; }

	[JsonPropertyName("minDps")]
	public double MinDps { get; set; }

	[JsonPropertyName("maxDps")]
	public double MaxDps { get; set; }

	[JsonPropertyName("avgDps")]
	public double AvgDps { get; set; }

	[JsonPropertyName("contributionBasis")]
	public string ContributionBasis { get; set; } = "partyDamage";

	[JsonPropertyName("contributionDenominator")]
	public long ContributionDenominator { get; set; }

	[JsonPropertyName("contributionReliable")]
	public bool ContributionReliable { get; set; } = true;

	[JsonPropertyName("playerCount")]
	public int PlayerCount { get; set; }

	[JsonPropertyName("fightMeta")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public FightMeta? FightMeta { get; set; }

	[JsonPropertyName("markers")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<FightMarker>? Markers { get; set; }

	[JsonPropertyName("players")]
	public List<PlayerSessionData> Players { get; set; } = new List<PlayerSessionData>();
}
