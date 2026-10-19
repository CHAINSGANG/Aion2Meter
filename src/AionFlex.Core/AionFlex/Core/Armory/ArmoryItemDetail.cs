using System.Collections.Generic;

namespace AionFlex.Core.Armory;

public sealed record ArmoryItemDetail(string Name, string Grade, string Category, int Level, int EnchantLevel, int MaxEnchantLevel, IReadOnlyList<ArmoryStat> MainStats, IReadOnlyList<ArmoryStat> SubStats, IReadOnlyList<ArmoryManastone> Manastones, int ManastoneSlots, int GodstoneSlots, string? SoulBindRate);
