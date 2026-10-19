namespace AionFlex.Core.Parsing;

public readonly record struct InventoryItem(ulong Handle, uint ItemId, ulong Count, byte Container, byte Slot);
