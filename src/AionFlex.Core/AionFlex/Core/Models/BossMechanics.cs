using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class BossMechanics
{
	public required string Id { get; init; }

	public required string DungeonKey { get; init; }

	public MechanicCategory Category { get; init; }

	public int Order { get; init; }

	public IReadOnlyList<int> NpcIds { get; init; } = Array.Empty<int>();

	public required LocalizedText Name { get; init; }

	public IReadOnlyList<BossMechanic> Mechanics { get; init; } = Array.Empty<BossMechanic>();
}
