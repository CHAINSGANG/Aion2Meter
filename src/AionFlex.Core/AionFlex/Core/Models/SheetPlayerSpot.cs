namespace AionFlex.Core.Models;

public sealed class SheetPlayerSpot
{
	public required int Number { get; init; }

	public double X { get; init; }

	public double Y { get; init; }

	public LocalizedText? Note { get; init; }

	public SheetTeam? Team { get; init; }
}
