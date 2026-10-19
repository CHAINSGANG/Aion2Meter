using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record GenusInsightSnapshot(IReadOnlyList<GenusBlock> Genera)
{
	public static readonly GenusInsightSnapshot Empty = new GenusInsightSnapshot(Array.Empty<GenusBlock>());
}
