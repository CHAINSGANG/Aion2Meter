using System;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;

namespace AionFlex.Core.LootEnergy;

public static class LootEnergyDisplay
{
	public const int DailyMax = 560;

	public const int SubscribedDailyMax = 840;

	public const int BonusMax = 2000;

	public const uint BonusArgb = 4281644236u;

	public static bool IsAvailable(Func<Feature, bool> isEnabled)
	{
		return isEnabled(Feature.LootEnergy);
	}

	public static string Text(LootEnergyReading r)
	{
		return $"{r.Daily} (+{r.Bonus})";
	}

	public static string DailyText(LootEnergyReading r)
	{
		return $"{r.Daily} ";
	}

	public static string BonusText(LootEnergyReading r)
	{
		return $"(+{r.Bonus})";
	}

	public static string Tooltip(LootEnergyReading r)
	{
		return $"Loot energy (Odyle): daily {r.Daily} + stone bonus {r.Bonus}\nDaily max {560} ({840} with the energy-charge subscription), stone bonus max {2000}.\n" + "Updated on login, on zone change and when it changes.";
	}
}
