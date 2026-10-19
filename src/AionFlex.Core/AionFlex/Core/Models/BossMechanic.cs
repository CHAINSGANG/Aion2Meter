namespace AionFlex.Core.Models;

public sealed class BossMechanic
{
	public required string Id { get; init; }

	public required MechanicTrigger Trigger { get; init; }

	public MechanicSeverity Severity { get; init; }

	public required LocalizedText Action { get; init; }

	public LocalizedText? Detail { get; init; }

	public MechanicsSheet? Sheet { get; init; }

	public MechanicLiveTrigger? LiveTrigger { get; init; }
}
