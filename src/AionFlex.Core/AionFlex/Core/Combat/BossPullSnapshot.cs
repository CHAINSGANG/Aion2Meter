namespace AionFlex.Core.Combat;

public readonly record struct BossPullSnapshot(long? PullFirstHitMs, long PullDamage, long RecentDamage, long? EnrageObservedAtMs);
