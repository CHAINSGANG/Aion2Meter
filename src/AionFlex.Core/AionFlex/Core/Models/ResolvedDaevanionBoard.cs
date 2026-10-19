using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedDaevanionBoard(int BoardId, string BoardName, int ActivatedTotal, int UnlockedTotal, int PopulatedTotal, string CompletionDisplay, int CompletionPercent, IReadOnlyList<ResolvedDaevanionStatEffect> StatEffects, IReadOnlyList<ResolvedDaevanionSkillEffect> SkillEffects);
