namespace AionFlex.Core.Models;

public sealed record FieldBossEntry(int RowId, bool Alive, FieldBossPosition? Position, long TimeUtcMs);
