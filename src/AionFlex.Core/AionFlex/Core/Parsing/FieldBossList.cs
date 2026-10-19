using System.Collections.Generic;

namespace AionFlex.Core.Parsing;

public sealed record FieldBossList(int MapId, IReadOnlyList<FieldBossListEntry> Entries);
