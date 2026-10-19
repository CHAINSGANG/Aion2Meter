using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record WingSnapshot(IReadOnlyList<WingEntry> OwnedWings)
{
	public static readonly WingSnapshot Empty = new WingSnapshot(Array.Empty<WingEntry>());
}
