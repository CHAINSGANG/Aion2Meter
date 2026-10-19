using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedWingEntry(long WingId, string Name, int Grade, string GradeName, int Level, IReadOnlyList<ResolvedWingEffect> EquipStats);
