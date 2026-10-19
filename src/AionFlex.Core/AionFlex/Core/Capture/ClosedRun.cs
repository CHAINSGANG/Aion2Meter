namespace AionFlex.Core.Capture;

public sealed record ClosedRun(int Sequence, long AnchorMs, RunAnchorKind AnchorKind, long CloseMs, RunCloseKind CloseKind, long LastActivityMs, int? ServerId, int? DungeonId, long? LiveStartMs, long? EndedMs, long? ResultMs, bool? Cleared);
