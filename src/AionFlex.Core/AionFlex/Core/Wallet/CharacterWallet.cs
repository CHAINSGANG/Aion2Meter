namespace AionFlex.Core.Wallet;

public sealed record CharacterWallet(string Character, long BoundKina, long TradableKina, long AbyssPoints, long ReadAtMs, string? Region, int? ServerId);
