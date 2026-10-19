namespace AionFlex.Core.Models;

public sealed record LootEnergyEvent(int Daily, int Bonus, bool FromLoginList, long TimestampMs);
