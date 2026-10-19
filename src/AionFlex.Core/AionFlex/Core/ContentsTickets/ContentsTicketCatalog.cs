using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AionFlex.Core.ContentsTickets;

public sealed class ContentsTicketCatalog
{
	private readonly Dictionary<uint, ContentsTicketInfo> _rows;

	public static ContentsTicketCatalog Empty { get; } = new ContentsTicketCatalog(new Dictionary<uint, ContentsTicketInfo>());

	public int Count => _rows.Count;

	private ContentsTicketCatalog(Dictionary<uint, ContentsTicketInfo> rows)
	{
		_rows = rows;
	}

	public ContentsTicketInfo? Get(uint id)
	{
		if (!_rows.TryGetValue(id, out ContentsTicketInfo value))
		{
			return null;
		}
		return value;
	}

	public static string? TableRegionOf(string? clientRegion)
	{
		if (clientRegion == "Global")
		{
			return "Global";
		}
		return null;
	}

	public static IReadOnlyDictionary<string, ContentsTicketCatalog> ParseAll(string json)
	{
		Dictionary<string, ContentsTicketCatalog> dictionary = new Dictionary<string, ContentsTicketCatalog>(StringComparer.Ordinal);
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		foreach (JsonProperty item in jsonDocument.RootElement.EnumerateObject())
		{
			if (item.Name.StartsWith('_') || item.Value.ValueKind != JsonValueKind.Object)
			{
				continue;
			}
			Dictionary<uint, ContentsTicketInfo> dictionary2 = new Dictionary<uint, ContentsTicketInfo>();
			foreach (JsonProperty item2 in item.Value.EnumerateObject())
			{
				if (uint.TryParse(item2.Name, out var result) && TryRead(result, item2.Value, out ContentsTicketInfo info))
				{
					dictionary2[result] = info;
				}
			}
			dictionary[item.Name] = new ContentsTicketCatalog(dictionary2);
		}
		return dictionary;
	}

	public static IReadOnlyDictionary<string, ContentsTicketCatalog> LoadAll(string path)
	{
		try
		{
			IReadOnlyDictionary<string, ContentsTicketCatalog> result;
			if (!File.Exists(path))
			{
				IReadOnlyDictionary<string, ContentsTicketCatalog> readOnlyDictionary = new Dictionary<string, ContentsTicketCatalog>();
				result = readOnlyDictionary;
			}
			else
			{
				result = ParseAll(File.ReadAllText(path));
			}
			return result;
		}
		catch (Exception)
		{
			return new Dictionary<string, ContentsTicketCatalog>();
		}
	}

	private static bool TryRead(uint id, JsonElement e, out ContentsTicketInfo info)
	{
		info = null;
		string text = Text("en");
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		List<ContentsTicketRefill> list = new List<ContentsTicketRefill>();
		if (e.TryGetProperty("daily", out var value) && TryTime(value, "at", out var time))
		{
			list.Add(new ContentsTicketRefill(Int(value, "count"), time));
		}
		if (e.TryGetProperty("weekly", out var value2) && TryTime(value2, "at", out var time2) && value2.TryGetProperty("day", out var value3) && Enum.TryParse<DayOfWeek>(value3.GetString(), out var result))
		{
			list.Add(new ContentsTicketRefill(Int(value2, "count"), time2, result));
		}
		if (e.TryGetProperty("every", out var value4) && TryTime(value4, "from", out var time3) && Int(value4, "seconds") > 0)
		{
			int count = Int(value4, "count");
			TimeOnly at = time3;
			int everySeconds = Int(value4, "seconds");
			list.Add(new ContentsTicketRefill(count, at, null, everySeconds));
		}
		info = new ContentsTicketInfo(id, Text("internal") ?? "", text, Text("ko"), Text("zh"), Text("type") == "Time", Int(e, "max"), Int(e, "subscribeMax"), e.TryGetProperty("showMax", out var value5) && value5.ValueKind == JsonValueKind.True, Text("unlock") ?? "", list);
		return true;
		static int Int(JsonElement o, string key)
		{
			if (!o.TryGetProperty(key, out var value6) || !value6.TryGetInt32(out var value7))
			{
				return 0;
			}
			return value7;
		}
		string? Text(string key)
		{
			if (!e.TryGetProperty(key, out var value6) || value6.ValueKind != JsonValueKind.String)
			{
				return null;
			}
			return value6.GetString();
		}
	}

	private static bool TryTime(JsonElement o, string key, out TimeOnly time)
	{
		time = default;
		if (o.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
		{
			return TimeOnly.TryParseExact(value.GetString(), "HH:mm", out time);
		}
		return false;
	}
}
