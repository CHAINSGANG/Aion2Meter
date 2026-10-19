namespace AionFlex.Core.Models;

public sealed class ChecklistItem
{
	public string Id { get; init; } = "";

	public string Name { get; init; } = "";

	public string Description { get; init; } = "";

	public bool IsWeekly { get; init; }

	public bool IsChecked { get; set; }
}
