using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class MechanicLiveTrigger
{
	public required MechanicLiveTriggerKind Kind { get; init; }

	public IReadOnlyList<int> SkillCodes { get; init; } = Array.Empty<int>();

	public IReadOnlyList<int> NpcIds { get; init; } = Array.Empty<int>();
}
