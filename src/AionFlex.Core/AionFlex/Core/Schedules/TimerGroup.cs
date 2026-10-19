using System.Collections.Generic;

namespace AionFlex.Core.Schedules;

public sealed record TimerGroup(TimerBucket Bucket, string Title, IReadOnlyList<TimerItem> Items);
