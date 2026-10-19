using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public readonly record struct InventoryItemChange(InventoryItemOp Op, InventoryItem Item);
