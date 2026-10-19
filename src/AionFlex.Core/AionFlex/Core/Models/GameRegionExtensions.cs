using System.Collections.Generic;
using AionFlex.Core.Servers;

namespace AionFlex.Core.Models;

public static class GameRegionExtensions
{
	public static IReadOnlyList<GameRegion> All { get; } = new _003C_003Ez__ReadOnlyArray<GameRegion>(new GameRegion[7]
	{
		GameRegion.TW,
		GameRegion.KR,
		GameRegion.EU,
		GameRegion.NA,
		GameRegion.NAW,
		GameRegion.LA,
		GameRegion.AS
	});

	public static string ServiceOf(this GameRegion region)
	{
		return region switch
		{
			GameRegion.TW => "TW", 
			GameRegion.KR => "KR", 
			_ => "Global", 
		};
	}

	public static string DisplayName(this GameRegion region)
	{
		return region switch
		{
			GameRegion.TW => "Taiwan", 
			GameRegion.KR => "Korea", 
			GameRegion.EU => "Global · Europe", 
			GameRegion.NA => "Global · North America East", 
			GameRegion.NAW => "Global · North America West", 
			GameRegion.LA => "Global · South America", 
			GameRegion.AS => "Global · Asia", 
			_ => region.ToString(), 
		};
	}

	public static GameRegion? GlobalDatacenterOf(int serverId)
	{
		if (!ServerIdSpace.IsGlobalServerId(serverId))
		{
			return null;
		}
		return (serverId / 100 % 10) switch
		{
			1 => (GameRegion?)GameRegion.NA, 
			2 => GameRegion.NAW, 
			3 => GameRegion.EU, 
			4 => GameRegion.LA, 
			5 => GameRegion.AS, 
			_ => null, 
		};
	}

	public static GameRegion? TryFromClient(string? regionCode, int? serverId)
	{
		switch (regionCode)
		{
		case "TW":
			return GameRegion.TW;
		case "KR":
			return GameRegion.KR;
		case "Global":
		{
			GameRegion? result;
			if (serverId.HasValue)
			{
				int valueOrDefault = serverId.GetValueOrDefault();
				result = GlobalDatacenterOf(valueOrDefault);
			}
			else
			{
				result = null;
			}
			return result;
		}
		default:
			return null;
		}
	}
}
