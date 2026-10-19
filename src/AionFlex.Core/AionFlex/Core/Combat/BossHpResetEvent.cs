namespace AionFlex.Core.Combat;

public readonly record struct BossHpResetEvent(int EntityId, long TimestampMs, uint PreviousHp, uint NewHp, uint MaxHp);
