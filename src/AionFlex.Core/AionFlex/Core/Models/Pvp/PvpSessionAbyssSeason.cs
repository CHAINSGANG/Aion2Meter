using System;
using System.Text.Json.Serialization;

namespace AionFlex.Core.Models.Pvp;

public sealed class PvpSessionAbyssSeason
{
	[JsonPropertyName("grade")]
	public int Grade { get; set; }

	[JsonPropertyName("rankingPoints")]
	public long RankingPoints { get; set; }

	[JsonPropertyName("kills")]
	public int Kills { get; set; }

	[JsonPropertyName("deaths")]
	public int Deaths { get; set; }

	[JsonPropertyName("assists")]
	public int Assists { get; set; }

	[JsonPropertyName("rankDelta")]
	public int RankDelta { get; set; }

	[JsonPropertyName("boardSnapshotAt")]
	public DateTime? BoardSnapshotAt { get; set; }
}
