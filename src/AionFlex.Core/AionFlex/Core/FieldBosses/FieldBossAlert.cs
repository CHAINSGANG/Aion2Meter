using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Schedules;

namespace AionFlex.Core.FieldBosses;

public sealed record FieldBossAlert(EventAlertPhase Phase, long AtUtcMs, TimeSpan TimeUntil, IReadOnlyList<(FieldBossInfo Boss, long AtUtcMs)> Bosses)
{
	public string OccurrenceKey => $"{Phase}:{AtUtcMs}:{string.Join(",", from b in Bosses
		select b.Boss.RowId into id
		orderby id
		select id)}";
}
