using System;

namespace AionFlex.Core.Schedules;

public sealed record ServerClockZone(TimeZoneInfo Zone, string Label, bool FromGame, bool DisagreesWithNamedZone);
