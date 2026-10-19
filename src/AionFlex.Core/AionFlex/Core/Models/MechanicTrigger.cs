using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class MechanicTrigger
{
	public required MechanicTriggerType Type { get; init; }

	public double HpPercent { get; init; }

	public double? PreWarnPct { get; init; }

	public LocalizedText? Label { get; init; }

	public IReadOnlyList<int> SkillCodes { get; init; } = Array.Empty<int>();
}
