using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AionFlex.Core.Armory;

public static class ArmoryJson
{
	private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);

	public static IReadOnlyList<ArmoryCharacter> ParseSearch(string json)
	{
		List<ArmoryCharacter> list = new List<ArmoryCharacter>();
		if (!TryParse(json, out JsonDocument doc))
		{
			return list;
		}
		using (doc)
		{
			if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("list", out var value) || value.ValueKind != JsonValueKind.Array)
			{
				return list;
			}
			foreach (JsonElement item in value.EnumerateArray())
			{
				string text = Str(item, "characterId");
				string text2 = Str(item, "name");
				int num = Int(item, "serverId");
				if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2) && num > 0)
				{
					list.Add(new ArmoryCharacter(text, num, Str(item, "serverName") ?? "", Tags.Replace(text2, ""), Int(item, "level"), Int(item, "pcId")));
				}
			}
			return list;
		}
	}

	public static ArmoryEquipment? ParseEquipment(string json)
	{
		if (!TryParse(json, out JsonDocument doc))
		{
			return null;
		}
		using (doc)
		{
			JsonElement rootElement = doc.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object)
			{
				return null;
			}
			JsonElement value = default;
			if (((!rootElement.TryGetProperty("equipment", out var value2) || value2.ValueKind != JsonValueKind.Object || !value2.TryGetProperty("equipmentList", out value)) && !rootElement.TryGetProperty("equipmentList", out value)) || value.ValueKind != JsonValueKind.Array)
			{
				return null;
			}
			List<ArmoryItem> list = new List<ArmoryItem>();
			foreach (JsonElement item in value.EnumerateArray())
			{
				int num = Int(item, "id");
				string text = Str(item, "name");
				if (num > 0 && !string.IsNullOrEmpty(text))
				{
					list.Add(new ArmoryItem(num, text, Int(item, "enchantLevel"), Int(item, "exceedLevel"), Str(item, "grade") ?? "", Int(item, "slotPos"), Str(item, "slotPosName") ?? "", Str(item, "icon")));
				}
			}
			JsonElement parent = ((rootElement.TryGetProperty("petwing", out var value3) && value3.ValueKind == JsonValueKind.Object) ? value3 : rootElement);
			return new ArmoryEquipment(list.OrderBy((ArmoryItem i) => i.SlotPos).ToList(), Companion(parent, "pet"), Companion(parent, "wing"));
		}
	}

	public static ArmoryItemDetail? ParseItemDetail(string json)
	{
		if (!TryParse(json, out JsonDocument doc))
		{
			return null;
		}
		using (doc)
		{
			JsonElement rootElement = doc.RootElement;
			if (rootElement.ValueKind != JsonValueKind.Object)
			{
				return null;
			}
			string text = Str(rootElement, "name");
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			List<ArmoryManastone> list = new List<ArmoryManastone>();
			if (rootElement.TryGetProperty("magicStoneStat", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value.EnumerateArray())
				{
					string text2 = Str(item, "name");
					if (text2 != null && text2.Length > 0)
					{
						list.Add(new ArmoryManastone(text2, Str(item, "value") ?? "", Str(item, "grade")));
					}
				}
			}
			return new ArmoryItemDetail(text, Str(rootElement, "gradeName") ?? Str(rootElement, "grade") ?? "", Str(rootElement, "categoryName") ?? "", Int(rootElement, "level"), Int(rootElement, "enchantLevel"), Int(rootElement, "maxEnchantLevel"), Stats(rootElement, "mainStats"), Stats(rootElement, "subStats"), list, Int(rootElement, "magicStoneSlotCount"), Int(rootElement, "godStoneSlotCount"), Str(rootElement, "soulBindRate"));
		}
	}

	private static IReadOnlyList<ArmoryStat> Stats(JsonElement e, string property)
	{
		List<ArmoryStat> list = new List<ArmoryStat>();
		if (!e.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return list;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			string text = Str(item, "name");
			if (text != null && text.Length > 0)
			{
				list.Add(new ArmoryStat(text, Str(item, "value") ?? "", Str(item, "minValue"), Str(item, "extra")));
			}
		}
		return list;
	}

	private static ArmoryCompanion? Companion(JsonElement parent, string property)
	{
		if (!parent.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return null;
		}
		string text = Str(value, "name");
		if (!string.IsNullOrEmpty(text))
		{
			return new ArmoryCompanion(text, Int(value, "level"), Int(value, "enchantLevel"), Str(value, "grade"), Str(value, "icon"));
		}
		return null;
	}

	private static bool TryParse(string? json, out JsonDocument doc)
	{
		doc = null;
		if (string.IsNullOrWhiteSpace(json))
		{
			return false;
		}
		try
		{
			doc = JsonDocument.Parse(json);
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static string? Str(JsonElement e, string property)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var value))
		{
			return value.ValueKind switch
			{
				JsonValueKind.String => value.GetString(), 
				JsonValueKind.Number => value.GetRawText(), 
				_ => null, 
			};
		}
		return null;
	}

	private static int Int(JsonElement e, string property)
	{
		int result;
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var value))
		{
			JsonValueKind valueKind = value.ValueKind;
			if (valueKind != JsonValueKind.String)
			{
				if (valueKind != JsonValueKind.Number || !value.TryGetInt32(out var value2))
				{
					goto IL_0054;
				}
				result = value2;
			}
			else
			{
				if (!int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
				{
					goto IL_0054;
				}
				result = result2;
			}
			goto IL_0057;
		}
		return 0;
		IL_0054:
		result = 0;
		goto IL_0057;
		IL_0057:
		return result;
	}
}
