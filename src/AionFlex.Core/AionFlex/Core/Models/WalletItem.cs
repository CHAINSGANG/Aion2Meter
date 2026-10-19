namespace AionFlex.Core.Models;

public readonly record struct WalletItem(WalletCurrency Currency, ulong Handle, InventoryItemOp Op, long Count);
