namespace AionFlex.Core.Models;

public sealed record GearChange(int EntityId, GearItem? OldItem, GearItem? NewItem);
