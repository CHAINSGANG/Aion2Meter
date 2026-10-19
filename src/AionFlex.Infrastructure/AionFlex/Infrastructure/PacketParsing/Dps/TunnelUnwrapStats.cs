namespace AionFlex.Infrastructure.PacketParsing.Dps;

public readonly record struct TunnelUnwrapStats(long StreamsDetected, long TunnelPayloads, long DataFrames, long Delivered, long Duplicates, long ControlFrames, long BadFrames, long MissingIdentity, long UnrecognisedFrames, long Desyncs, long Resyncs, long ResyncSkippedBytes, long HeldFrames, long LateFills, long GapsSkipped, long SkippedSequences, long LostAfterSkip, long MaxHoldMs, int MaxHeldBytes, long HeldBytesNow, long StreamsEvicted, long ConnectionsSeen, long ConnectionsEvicted, int MaxRetainedBytes, long Errors);
