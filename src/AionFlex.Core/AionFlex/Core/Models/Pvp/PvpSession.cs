using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSession
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	[JsonPropertyName("sessionType")]
	public string SessionType { get; set; } = "pvp";

	[JsonPropertyName("sessionId")]
	public string SessionId { get; set; } = Guid.NewGuid().ToString();

	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	[JsonPropertyName("startedAt")]
	public DateTime? StartedAt { get; set; }

	[JsonPropertyName("battleTimeMs")]
	public long BattleTimeMs { get; set; }

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

	[JsonPropertyName("instanceName")]
	public string? InstanceName { get; set; }

	[JsonPropertyName("instanceKey")]
	public string? InstanceKey { get; set; }

	[JsonPropertyName("contentType")]
	public string ContentType { get; set; } = "openWorldPvp";

	[JsonPropertyName("kills")]
	public int Kills { get; set; }

	[JsonPropertyName("creditedKills")]
	public int CreditedKills { get; set; }

	[JsonPropertyName("deaths")]
	public int Deaths { get; set; }

	[JsonPropertyName("opponentCount")]
	public int OpponentCount { get; set; }

	[JsonPropertyName("totalDamageDealt")]
	public long TotalDamageDealt { get; set; }

	[JsonPropertyName("totalDamageTaken")]
	public long TotalDamageTaken { get; set; }

	[JsonPropertyName("local")]
	public PvpSessionLocal Local { get; set; } = new PvpSessionLocal();

	[JsonPropertyName("opponents")]
	public List<PvpSessionOpponent> Opponents { get; set; } = new List<PvpSessionOpponent>();
}
