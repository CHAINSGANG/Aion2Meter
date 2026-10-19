using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionDpsPoint
{
	[JsonPropertyName("o")]
	public long OffsetMs { get; set; }

	[JsonPropertyName("d")]
	public double Dps { get; set; }
}
