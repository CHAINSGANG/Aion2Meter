namespace AionFlex.Core.Models;

public sealed record DungeonRunStateEvent(int DungeonId, int State, long StartMs, long EndMs, long TimestampMs);
