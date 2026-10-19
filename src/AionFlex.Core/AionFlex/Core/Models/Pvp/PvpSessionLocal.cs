using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionLocal
{
	[JsonPropertyName("entityId")]
	public int EntityId { get; set; }

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("hpCurrent")]
	public long HpCurrent { get; set; }

	[JsonPropertyName("hpMax")]
	public long HpMax { get; set; }

	[JsonPropertyName("lastKillerId")]
	public int? LastKillerId { get; set; }

	[JsonPropertyName("lastKillerName")]
	public string? LastKillerName { get; set; }

	[JsonPropertyName("abyssSeason")]
	public PvpSessionAbyssSeason? AbyssSeason { get; set; }
}
