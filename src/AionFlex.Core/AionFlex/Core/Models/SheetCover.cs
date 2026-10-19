namespace AionFlex.Core.Models;

public sealed class SheetCover
{
	public double X { get; init; }

	public double Y { get; init; }

	public bool Active { get; init; }

	public LocalizedText? Label { get; init; }
}
