using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ContentsTicketsEvent(IReadOnlyList<ContentsTicketEntry> Entries, bool FromLoginList, long TimestampMs);
