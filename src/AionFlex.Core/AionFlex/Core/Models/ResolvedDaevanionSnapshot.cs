using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedDaevanionSnapshot(ResolvedDaevanionSummary Summary, IReadOnlyList<ResolvedDaevanionBoard> Boards);
