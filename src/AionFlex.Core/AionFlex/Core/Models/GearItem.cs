using System.Collections.Generic;

namespace AionFlex.Core.Models;

public readonly record struct GearItem(int ItemId, int EnchantLevel, int Slot, IReadOnlyList<byte> MetaRaw, IReadOnlyList<GearSubStat> SubStats, IReadOnlyList<GearSkillSubStat> SkillSubStats);
