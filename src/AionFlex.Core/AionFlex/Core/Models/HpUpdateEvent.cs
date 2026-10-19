using System;

namespace AionFlex.Core.Models;

public sealed class HpUpdateEvent
{
	public Guid Id { get; init; } = Guid.NewGuid();

	public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public int ActorId { get; init; }

	public uint CurrentHp { get; init; }

	public uint? MaxHp { get; init; }

	public HpReadingSource Source { get; init; }

	public uint? FramedSpawnMaxHp { get; init; }

	public int? SpawnNpcId { get; init; }

	public override string ToString()
	{
		string value = (MaxHp.HasValue ? $"/{MaxHp.Value}" : "");
		return $"HpUpdateEvent[Actor={ActorId}, HP={CurrentHp}{value}, {Source}]";
	}
}
