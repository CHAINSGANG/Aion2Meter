namespace AionFlex.Core.Combat;

public sealed record HealingBreakdownTarget(int TargetId, string Name, long Total, int Hits, double SharePct, int SummonCount = 0);
