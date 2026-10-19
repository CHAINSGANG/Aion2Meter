using AionFlex.Core.Localization;

namespace AionFlex.Core.Models;

public sealed record EventText(string En, string Ko, string Zh)
{
	public static EventText Same(string text)
	{
		return new EventText(text, text, text);
	}

	public string For(Language language)
	{
		return language switch
		{
			Language.Korean => Ko, 
			Language.Chinese => Zh, 
			_ => En, 
		};
	}

	public override string ToString()
	{
		return En;
	}
}
