using System;

namespace AionFlex.Core.Models;

public sealed class GroggyGuardEvent
{
	public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public int ActorId { get; init; }

	public uint MaxGuard { get; init; }

	public uint CurrentGuard { get; init; }
}
