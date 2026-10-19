using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class ObservedBuff
{
	public int ApplyCount { get; init; }

	public int SelfCastApplyCount { get; init; }

	public int CastByMeCount { get; init; }

	public int CastByMeProcCount { get; init; }

	public IReadOnlyList<TimeRangeMs> Intervals { get; init; } = Array.Empty<TimeRangeMs>();
}
