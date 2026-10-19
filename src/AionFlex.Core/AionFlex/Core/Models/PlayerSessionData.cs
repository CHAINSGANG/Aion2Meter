using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class PlayerSessionData
{
	public const int RotationMaxEntries = 1500;

	[JsonPropertyName("entityId")]
	public int EntityId { get; set; }

	[JsonPropertyName("nickname")]
	public string Nickname { get; set; } = "";

	[JsonPropertyName("jobClass")]
	public string JobClass { get; set; } = "";

	[JsonPropertyName("totalDamage")]
	public long TotalDamage { get; set; }

	[JsonPropertyName("dpsDamage")]
	public long DpsDamage { get; set; }

	[JsonPropertyName("supportDamage")]
	public long SupportDamage { get; set; }

	[JsonPropertyName("healAmount")]
	public long HealAmount { get; set; }

	[JsonPropertyName("dps")]
	public double Dps { get; set; }

	[JsonPropertyName("activeTimeMs")]
	public long ActiveTimeMs { get; set; }

	[JsonPropertyName("damageContribution")]
	public double DamageContribution { get; set; }

	[JsonPropertyName("partyDamageShare")]
	public double PartyDamageShare { get; set; }

	[JsonPropertyName("totalHits")]
	public int TotalHits { get; set; }

	[JsonPropertyName("critHits")]
	public int CritHits { get; set; }

	[JsonPropertyName("qualifiedHits")]
	public int QualifiedHits { get; set; }

	[JsonPropertyName("backHits")]
	public int BackHits { get; set; }

	[JsonPropertyName("frontHits")]
	public int FrontHits { get; set; }

	[JsonPropertyName("doubleHits")]
	public int DoubleHits { get; set; }

	[JsonPropertyName("perfectHits")]
	public int PerfectHits { get; set; }

	[JsonPropertyName("critRate")]
	public double CritRate { get; set; }

	[JsonPropertyName("backRate")]
	public double BackRate { get; set; }

	[JsonPropertyName("doubleRate")]
	public double DoubleRate { get; set; }

	[JsonPropertyName("perfectRate")]
	public double PerfectRate { get; set; }

	[JsonPropertyName("highestHit")]
	public long HighestHit { get; set; }

	[JsonPropertyName("isLocalPlayer")]
	public bool IsLocalPlayer { get; set; }

	[JsonPropertyName("combatPower")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? CombatPower { get; set; }

	[JsonPropertyName("cpObservedAtMs")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? CpObservedAtMs { get; set; }

	[JsonPropertyName("cpSource")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? CpSource { get; set; }

	[JsonPropertyName("gearScore")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? GearScore { get; set; }

	[JsonPropertyName("level")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Level { get; set; }

	[JsonPropertyName("serverId")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? ServerId { get; set; }

	[JsonPropertyName("specCodes")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<int>? SpecCodes { get; set; }

	[JsonPropertyName("specStigmaCodes")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<int>? SpecStigmaCodes { get; set; }

	[JsonPropertyName("arcanaSetIds")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<string>? ArcanaSetIds { get; set; }

	[JsonPropertyName("loadoutObservedAtMs")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? LoadoutObservedAtMs { get; set; }

	[JsonPropertyName("defense")]
	public DefenseSessionData? Defense { get; set; }

	[JsonPropertyName("accuracy")]
	public AccuracySessionData? Accuracy { get; set; }

	[JsonPropertyName("rotation")]
	public List<RotationTile> Rotation { get; set; } = new List<RotationTile>();

	[JsonPropertyName("rotationTruncated")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool RotationTruncated { get; set; }

	[JsonPropertyName("dpsTimeline")]
	public List<DpsTimelinePoint> DpsTimeline { get; set; } = new List<DpsTimelinePoint>();

	[JsonPropertyName("skills")]
	public List<SkillSessionData> Skills { get; set; } = new List<SkillSessionData>();
}
