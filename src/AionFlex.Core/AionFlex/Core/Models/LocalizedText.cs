using AionFlex.Core.Localization;

namespace AionFlex.Core.Models;

public sealed class LocalizedText
{
	public string En { get; init; } = string.Empty;

	public string? Ko { get; init; }

	public string? Zh { get; init; }

	public string Get(Language language)
	{
		return language switch
		{
			Language.Korean => string.IsNullOrEmpty(Ko) ? En : Ko, 
			Language.Chinese => string.IsNullOrEmpty(Zh) ? En : Zh, 
			_ => En, 
		};
	}
}
