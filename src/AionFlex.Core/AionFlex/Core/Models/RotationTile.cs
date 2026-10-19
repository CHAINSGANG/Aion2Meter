using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class RotationTile
{
	[JsonPropertyName("skillCode")]
	public int SkillCode { get; set; }

	[JsonPropertyName("skillName")]
	public string SkillName { get; set; } = "";

	[JsonPropertyName("offsetMs")]
	public long OffsetMs { get; set; }

	[JsonPropertyName("damage")]
	public long Damage { get; set; }

	[JsonPropertyName("crit")]
	public bool IsCritical { get; set; }

	[JsonPropertyName("perfect")]
	public bool IsPerfect { get; set; }

	[JsonPropertyName("back")]
	public bool IsBackAttack { get; set; }

	[JsonPropertyName("dot")]
	public bool IsDoT { get; set; }
}
