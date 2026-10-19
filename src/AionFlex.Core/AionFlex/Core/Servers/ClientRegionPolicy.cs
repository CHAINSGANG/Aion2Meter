using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Servers;

public static class ClientRegionPolicy
{
	public const string Global = "Global";

	public static string? Resolve(IReadOnlyCollection<string?> installRegions, int? localServerId)
	{
		if (localServerId.HasValue)
		{
			int valueOrDefault = localServerId.GetValueOrDefault();
			if (ServerIdSpace.IsGlobalServerId(valueOrDefault))
			{
				return "Global";
			}
			if (ServerIdSpace.IsTwKrServerId(valueOrDefault))
			{
				return Agreed(installRegions.Where((string r) => r != "Global"));
			}
		}
		return Agreed(installRegions);
	}

	private static string? Agreed(IEnumerable<string?> regions)
	{
		string text = null;
		foreach (string region in regions)
		{
			if (!string.IsNullOrEmpty(region))
			{
				if (text == null)
				{
					text = region;
				}
				else if (!string.Equals(text, region, StringComparison.Ordinal))
				{
					return null;
				}
			}
		}
		return text;
	}
}
