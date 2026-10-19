using System.Text.Json.Serialization;

namespace AionFlex.Core.Hotkeys;

public sealed class HotkeySetting
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; } = true;

	[JsonPropertyName("modifiers")]
	public int Modifiers { get; set; }

	[JsonPropertyName("key")]
	public int Key { get; set; }
}
