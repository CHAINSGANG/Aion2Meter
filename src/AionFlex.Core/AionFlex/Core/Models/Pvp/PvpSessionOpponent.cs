using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionOpponent
{
	[JsonPropertyName("entityId")]
	public int EntityId { get; set; }

	[JsonPropertyName("nickname")]
	public string Nickname { get; set; } = "";

	[JsonPropertyName("jobClass")]
	public string JobClass { get; set; } = "";

	[JsonPropertyName("serverId")]
	public int? ServerId { get; set; }

	[JsonPropertyName("faction")]
	public string? Faction { get; set; }

	[JsonPropertyName("damageDealt")]
	public long DamageDealt { get; set; }

	[JsonPropertyName("damageTaken")]
	public long DamageTaken { get; set; }

	[JsonPropertyName("dealtDps")]
	public double DealtDps { get; set; }

	[JsonPropertyName("takenDps")]
	public double TakenDps { get; set; }

	[JsonPropertyName("engagedSeconds")]
	public double EngagedSeconds { get; set; }

	[JsonPropertyName("firstTradeAt")]
	public DateTime? FirstTradeAt { get; set; }

	[JsonPropertyName("lastTradeAt")]
	public DateTime? LastTradeAt { get; set; }

	[JsonPropertyName("hpMax")]
	public long HpMax { get; set; }

	[JsonPropertyName("hpLastSeen")]
	public long HpLastSeen { get; set; }

	[JsonPropertyName("burstWindowDamage")]
	public long BurstWindowDamage { get; set; }

	[JsonPropertyName("deletedPercent")]
	public double DeletedPercent { get; set; }

	[JsonPropertyName("killed")]
	public bool Killed { get; set; }

	[JsonPropertyName("killCredited")]
	public bool KillCredited { get; set; }

	[JsonPropertyName("killedAt")]
	public DateTime? KilledAt { get; set; }

	[JsonPropertyName("timeToKillSeconds")]
	public double? TimeToKillSeconds { get; set; }

	[JsonPropertyName("highestDealtHit")]
	public long HighestDealtHit { get; set; }

	[JsonPropertyName("highestTakenHit")]
	public long HighestTakenHit { get; set; }

	[JsonPropertyName("dealtHits")]
	public int DealtHits { get; set; }

	[JsonPropertyName("dealtCritHits")]
	public int DealtCritHits { get; set; }

	[JsonPropertyName("dealtQualifiedHits")]
	public int DealtQualifiedHits { get; set; }

	[JsonPropertyName("dealtBackHits")]
	public int DealtBackHits { get; set; }

	[JsonPropertyName("dealtDoubleHits")]
	public int DealtDoubleHits { get; set; }

	[JsonPropertyName("dealtPerfectHits")]
	public int DealtPerfectHits { get; set; }

	[JsonPropertyName("dealtCritRate")]
	public double DealtCritRate { get; set; }

	[JsonPropertyName("dealtBackRate")]
	public double DealtBackRate { get; set; }

	[JsonPropertyName("dealtDoubleRate")]
	public double DealtDoubleRate { get; set; }

	[JsonPropertyName("dealtPerfectRate")]
	public double DealtPerfectRate { get; set; }

	[JsonPropertyName("takenHits")]
	public int TakenHits { get; set; }

	[JsonPropertyName("takenCritHits")]
	public int TakenCritHits { get; set; }

	[JsonPropertyName("takenQualifiedHits")]
	public int TakenQualifiedHits { get; set; }

	[JsonPropertyName("takenBackHits")]
	public int TakenBackHits { get; set; }

	[JsonPropertyName("takenDoubleHits")]
	public int TakenDoubleHits { get; set; }

	[JsonPropertyName("takenPerfectHits")]
	public int TakenPerfectHits { get; set; }

	[JsonPropertyName("takenCritRate")]
	public double TakenCritRate { get; set; }

	[JsonPropertyName("takenBackRate")]
	public double TakenBackRate { get; set; }

	[JsonPropertyName("takenDoubleRate")]
	public double TakenDoubleRate { get; set; }

	[JsonPropertyName("takenPerfectRate")]
	public double TakenPerfectRate { get; set; }

	[JsonPropertyName("dealtSkills")]
	public List<PvpSessionSkill> DealtSkills { get; set; } = new List<PvpSessionSkill>();

	[JsonPropertyName("takenSkills")]
	public List<PvpSessionSkill> TakenSkills { get; set; } = new List<PvpSessionSkill>();

	[JsonPropertyName("accuracy")]
	public PvpSessionAccuracy? Accuracy { get; set; }

	[JsonPropertyName("defense")]
	public PvpSessionDefense? Defense { get; set; }

	[JsonPropertyName("rotation")]
	public List<PvpSessionRotationTile> Rotation { get; set; } = new List<PvpSessionRotationTile>();

	[JsonPropertyName("takenRotation")]
	public List<PvpSessionRotationTile> TakenRotation { get; set; } = new List<PvpSessionRotationTile>();

	[JsonPropertyName("dpsTimeline")]
	public List<PvpSessionDpsPoint> DpsTimeline { get; set; } = new List<PvpSessionDpsPoint>();

	[JsonPropertyName("rotationTruncated")]
	public bool RotationTruncated { get; set; }

	[JsonPropertyName("abyssGrade")]
	public int? AbyssGrade { get; set; }
}
