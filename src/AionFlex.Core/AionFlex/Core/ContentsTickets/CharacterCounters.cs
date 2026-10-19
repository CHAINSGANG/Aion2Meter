using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.ContentsTickets;

public sealed record CharacterCounters(string Character, IReadOnlyDictionary<uint, ContentsTicketEntry> Entries, long ReadAtMs, string? Region, int? ServerId = null);
