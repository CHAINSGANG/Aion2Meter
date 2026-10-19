namespace AionFlex.Core.Models;

public static class GenusTypeExtensions
{
	public static string DisplayName(this GenusType type)
	{
		return type switch
		{
			GenusType.Cogni => "Cogni", 
			GenusType.Fera => "Fera", 
			GenusType.Natura => "Natura", 
			GenusType.Varian => "Varian", 
			GenusType.Special => "Special", 
			_ => type.ToString(), 
		};
	}

	public static string CodeName(this GenusType type)
	{
		return type switch
		{
			GenusType.Cogni => "Intellect", 
			GenusType.Fera => "Feral", 
			GenusType.Natura => "Nature", 
			GenusType.Varian => "Trans", 
			GenusType.Special => "Special", 
			_ => type.ToString(), 
		};
	}
}
