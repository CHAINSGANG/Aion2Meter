using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record FieldBossListEvent(int MapId, IReadOnlyList<FieldBossEntry> Entries, long NowMs);
