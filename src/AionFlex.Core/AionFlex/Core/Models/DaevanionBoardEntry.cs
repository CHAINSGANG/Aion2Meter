using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record DaevanionBoardEntry(int BoardId, IReadOnlyList<int> NodeIds);
