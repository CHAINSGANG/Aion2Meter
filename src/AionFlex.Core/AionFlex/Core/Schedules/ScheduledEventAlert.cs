using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Schedules;

public sealed record ScheduledEventAlert(GameEventType Kind, EventAlertPhase Phase, DateTime LocalStart, TimeSpan TimeUntilStart, IReadOnlyList<EventOccurrence> Occurrences);
