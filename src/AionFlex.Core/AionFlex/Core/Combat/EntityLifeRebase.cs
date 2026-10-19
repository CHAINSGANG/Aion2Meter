namespace AionFlex.Core.Combat;

public readonly record struct EntityLifeRebase(int EntityId, long CutoffMs, long AppearMs, bool FromDeathRemoval, int PurgedEvents);
