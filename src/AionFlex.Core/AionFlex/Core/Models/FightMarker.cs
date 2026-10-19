using System.Text.Json.Serialization;

namespace AionFlex.Core.Models;

public sealed class FightMarker
{
	[JsonPropertyName("o")]
	public int OffsetMs { get; set; }

	[JsonIgnore]
	public FightMarkerKind Kind { get; set; }

	[JsonPropertyName("k")]
	public string KindName
	{
		get
		{
			return Kind switch
			{
				FightMarkerKind.Stagger => "stagger", 
				FightMarkerKind.StaggerBroken => "staggerBroken", 
				FightMarkerKind.Death => "death", 
				_ => "cast", 
			};
		}
		set
		{
			Kind = value switch
			{
				"stagger" => FightMarkerKind.Stagger, 
				"staggerBroken" => FightMarkerKind.StaggerBroken, 
				"death" => FightMarkerKind.Death, 
				_ => FightMarkerKind.Cast, 
			};
		}
	}

	[JsonPropertyName("t")]
	public string Text { get; set; } = "";

	[JsonPropertyName("id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public int Id { get; set; }
}
