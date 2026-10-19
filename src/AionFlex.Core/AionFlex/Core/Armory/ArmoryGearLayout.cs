using System;

namespace AionFlex.Core.Armory;

public static class ArmoryGearLayout
{
	public static GearGroup GroupOf(string slotPosName)
	{
		switch (slotPosName)
		{
		case "MainHand":
		case "SubHand":
		case "Shield":
		case "Weapon":
			return GearGroup.Weapons;
		case "Shoulder":
		case "Helmet":
		case "Gloves":
		case "Torso":
		case "Pants":
		case "Boots":
		case "Belt":
		case "Cape":
			return GearGroup.Armor;
		case "Necklace":
		case "Earring1":
		case "Earring2":
		case "Pendant":
		case "Amulet":
		case "Ring1":
		case "Ring2":
		case "Bracelet1":
		case "Bracelet2":
			return GearGroup.Accessories;
		default:
			if (slotPosName.StartsWith("Arcana", StringComparison.Ordinal))
			{
				return GearGroup.Arcana;
			}
			return GearGroup.Other;
		}
	}

	public static string SlotLabel(string slotPosName)
	{
		switch (slotPosName)
		{
		case "MainHand":
			return "Main hand";
		case "SubHand":
			return "Off hand";
		case "Shoulder":
			return "Shoulders";
		case "Torso":
			return "Chest";
		case "Earring1":
		case "Earring2":
			return "Earring";
		case "Ring1":
		case "Ring2":
			return "Ring";
		case "Bracelet1":
		case "Bracelet2":
			return "Bracelet";
		default:
			if (slotPosName.StartsWith("Arcana", StringComparison.Ordinal))
			{
				return "Arcana";
			}
			if (slotPosName.StartsWith("Rune", StringComparison.Ordinal))
			{
				return "Rune";
			}
			return slotPosName;
		}
	}

	public static int OrderOf(ArmoryItem item)
	{
		return item.SlotPosName switch
		{
			"MainHand" => 0, 
			"SubHand" => 1, 
			"Helmet" => 0, 
			"Shoulder" => 1, 
			"Torso" => 2, 
			"Pants" => 3, 
			"Gloves" => 4, 
			"Boots" => 5, 
			"Cape" => 6, 
			"Belt" => 7, 
			"Necklace" => 0, 
			"Amulet" => 1, 
			"Pendant" => 2, 
			"Earring1" => 3, 
			"Earring2" => 4, 
			"Ring1" => 5, 
			"Ring2" => 6, 
			"Bracelet1" => 7, 
			"Bracelet2" => 8, 
			_ => 100 + item.SlotPos, 
		};
	}

	public static string GradeColor(string? grade)
	{
		return grade switch
		{
			"Common" => "#FFA8B3BF", 
			"Rare" => "#FF4E8FE8", 
			"Legend" => "#FF6BD4A4", 
			"Unique" => "#FFCBA135", 
			"Epic" => "#FFF08A4B", 
			"Heroic" => "#FFB48BF0", 
			"Special" => "#FF7FE3F0", 
			_ => "#FF8FA0B3", 
		};
	}
}
