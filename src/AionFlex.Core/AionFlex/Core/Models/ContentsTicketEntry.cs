namespace AionFlex.Core.Models;

public readonly record struct ContentsTicketEntry(uint TicketId, byte Flags, ulong? Raw, int Basic, int Bonus);
