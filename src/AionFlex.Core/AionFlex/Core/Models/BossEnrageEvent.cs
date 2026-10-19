namespace AionFlex.Core.Models;

public sealed class BossEnrageEvent
{
	public const int FrenzyAbnormalId = 10000008;

	public long Timestamp { get; init; }

	public int EntityId { get; init; }

	public int AbnormalId { get; init; }
}
