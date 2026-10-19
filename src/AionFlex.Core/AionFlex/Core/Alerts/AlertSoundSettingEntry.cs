using System.Text.Json.Serialization;

namespace AionFlex.Core.Alerts;

public sealed class AlertSoundSettingEntry
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; }

	[JsonPropertyName("sound")]
	public string? Sound { get; set; }

	[JsonPropertyName("volume")]
	public int Volume { get; set; } = 80;

	public AlertSoundSetting ToSetting()
	{
		return new AlertSoundSetting(Enabled, Sound, Volume);
	}
}
