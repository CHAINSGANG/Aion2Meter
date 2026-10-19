namespace AionFlex.Core.Armory;

public sealed record ArmoryItem(int Id, string Name, int EnchantLevel, int ExceedLevel, string Grade, int SlotPos, string SlotPosName, string? IconUrl);
