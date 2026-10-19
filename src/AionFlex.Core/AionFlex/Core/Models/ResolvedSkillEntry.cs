using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedSkillEntry(int Code, string Name, int Level, IReadOnlyList<int> Specs);
