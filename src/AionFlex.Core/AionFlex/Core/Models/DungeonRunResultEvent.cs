namespace AionFlex.Core.Models;

public sealed record DungeonRunResultEvent(int DungeonId, bool Cleared, long TimestampMs);
