namespace AionFlex.Infrastructure.PacketParsing.Dps;

public readonly record struct PartyRoomRosterStats(long PlainRecords, long LaneRecords, long LaneRejected, long WithoutLocalPlayer, long Duplicates, long Emitted);
