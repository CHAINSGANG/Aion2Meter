using System;

namespace AionFlex.Core.Schedules;

public sealed record TimerItem(string Key, string Name, string Sub, TimerKind Kind, DateTime Start, DateTime End, bool UpNow = false);
