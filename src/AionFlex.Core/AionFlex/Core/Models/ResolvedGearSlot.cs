using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedGearSlot(int SlotIndex, string SlotLabel, int ItemId, string ItemName, string GradeLabel, int ItemLevel, int EnchantLevel, string EnchantDisplay, IReadOnlyList<ResolvedGearStatLine> MainStats, IReadOnlyList<ResolvedGearStatLine> EnchantBonusStats, IReadOnlyList<ResolvedGearStatLine> ExceedEnchantStats, IReadOnlyList<ResolvedGearStatLine> RolledSubStats, IReadOnlyList<ResolvedGearSkillSubStatLine> RolledSkillSubStats, string SocketsDisplay, string SubstatSlotsDisplay, string SetName);
