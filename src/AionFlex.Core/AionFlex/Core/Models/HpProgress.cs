namespace AionFlex.Core.Models;

public readonly record struct HpProgress(long ObservedMaxHp, long MinAliveHp, long MinAliveHpTimestampMs, bool ReachedZeroHp, long AttemptStartMs = 0L);
