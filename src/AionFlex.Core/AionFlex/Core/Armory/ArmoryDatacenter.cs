using System.Collections.Generic;
using AionFlex.Core.Models;
using AionFlex.Core.Servers;

namespace AionFlex.Core.Armory;

public static class ArmoryDatacenter
{
	public static string? ApiRegionOf(int serverId)
	{
		if (!ServerIdSpace.IsGlobalServerId(serverId))
		{
			return null;
		}
		return (serverId / 100 % 10) switch
		{
			1 => "nae", 
			2 => "naw", 
			3 => "eu", 
			4 => "la", 
			5 => "as", 
			_ => null, 
		};
	}

	public static IReadOnlyList<int> PcIdsOf(JobClass jobClass)
	{
		List<int> list = new List<int>(4);
		for (int i = 1; i <= 48; i++)
		{
			if (jobClass != JobClass.Unknown && PcClassMap.ToJobClass(i) == jobClass)
			{
				list.Add(i);
			}
		}
		return list;
	}
}
