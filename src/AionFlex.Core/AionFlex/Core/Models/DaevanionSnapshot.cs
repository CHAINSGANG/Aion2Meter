using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record DaevanionSnapshot(IReadOnlyList<DaevanionBoardEntry> Boards)
{
	public static readonly DaevanionSnapshot Empty = new DaevanionSnapshot(Array.Empty<DaevanionBoardEntry>());
}
