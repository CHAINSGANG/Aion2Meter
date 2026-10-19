namespace AionFlex.Core.Combat;

public readonly record struct EnrageTimerInput(int? BerserkSeconds, bool IsTrial, long? PullFirstHitMs, long NowMs, long BossCurrentHp, bool BossDead, long PullDamage, long RecentDamage, long? EnrageObservedAtMs);
