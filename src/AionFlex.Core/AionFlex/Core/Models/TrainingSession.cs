using System;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class TrainingSession
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	[JsonPropertyName("sessionId")]
	public string SessionId { get; set; } = Guid.NewGuid().ToString();

	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;

	[JsonPropertyName("startedAt")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public DateTime? StartedAt { get; set; }

	[JsonPropertyName("sessionName")]
	public string SessionName { get; set; } = "";

	[JsonPropertyName("groupId")]
	public string GroupId { get; set; } = "";

	[JsonPropertyName("notes")]
	public string? Notes { get; set; }

	[JsonPropertyName("sessionIndex")]
	public int SessionIndex { get; set; }

	[JsonPropertyName("dummyNpcId")]
	public int DummyNpcId { get; set; }

	[JsonPropertyName("sessionData")]
	public BossSession SessionData { get; set; } = new BossSession();
}
