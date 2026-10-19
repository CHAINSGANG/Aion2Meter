using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionRotationTile
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
	public bool Crit { get; set; }

	[JsonPropertyName("perfect")]
	public bool Perfect { get; set; }

	[JsonPropertyName("back")]
	public bool Back { get; set; }

	[JsonPropertyName("dot")]
	public bool Dot { get; set; }
}
