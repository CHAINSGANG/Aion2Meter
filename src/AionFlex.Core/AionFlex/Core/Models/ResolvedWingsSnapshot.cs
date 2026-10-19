using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedWingsSnapshot(long? EquippedWingId, string? EquippedWingName, IReadOnlyList<ResolvedWingEffect> EquipEffect, IReadOnlyList<ResolvedWingEntry> OwnedWings, IReadOnlyList<ResolvedWingEffect> AllOwnedEffects);
