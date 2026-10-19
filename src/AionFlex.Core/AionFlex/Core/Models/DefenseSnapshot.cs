using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class DefenseSnapshot
{
	public DefenseCounters OutgoingAggregate { get; init; } = new DefenseCounters();

	public DefenseCounters IncomingAggregate { get; init; } = new DefenseCounters();

	public IReadOnlyDictionary<int, DefenseCounters> OutgoingPerSkill { get; init; } = new Dictionary<int, DefenseCounters>();

	public IReadOnlyList<long> IncomingBlockMomentsMs { get; init; } = Array.Empty<long>();

	public IReadOnlyList<long> IncomingHitMomentsMs { get; init; } = Array.Empty<long>();

	public IReadOnlyList<long> OutgoingHitMomentsMs { get; init; } = Array.Empty<long>();

	public IReadOnlyList<long> OutgoingFrontHitMomentsMs { get; init; } = Array.Empty<long>();

	public int OutgoingDirectionMeasuredHits { get; init; }

	public bool OutgoingMomentsTruncated { get; init; }

	public IReadOnlyList<TimeRangeMs> ActivitySegments { get; init; } = Array.Empty<TimeRangeMs>();

	public bool HasData
	{
		get
		{
			if (OutgoingAggregate.TotalAttempts <= 0)
			{
				return IncomingAggregate.TotalAttempts > 0;
			}
			return true;
		}
	}

	public static DefenseSnapshot Empty { get; } = new DefenseSnapshot();
}
