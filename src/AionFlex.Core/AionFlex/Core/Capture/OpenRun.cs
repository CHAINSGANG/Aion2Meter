namespace AionFlex.Core.Capture;

public sealed record OpenRun(int Sequence, long AnchorMs, RunAnchorKind AnchorKind, int? ServerId, int? DungeonId, long? LiveStartMs);
