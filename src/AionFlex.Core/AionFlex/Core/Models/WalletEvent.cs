using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record WalletEvent(IReadOnlyList<WalletItem> Items, bool FromLoginList, long TimestampMs);
