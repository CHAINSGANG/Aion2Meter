using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class DpsTimelinePoint
{
	[JsonPropertyName("o")]
	public long OffsetMs { get; set; }

	[JsonPropertyName("d")]
	public double Dps { get; set; }

	[JsonPropertyName("c")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? CumulativeDamage { get; set; }
}
