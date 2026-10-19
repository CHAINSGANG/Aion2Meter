using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record PlayerStatsSnapshot(int EntityId, IReadOnlyDictionary<int, long> Stats, long ReceivedAtUnixMs);
