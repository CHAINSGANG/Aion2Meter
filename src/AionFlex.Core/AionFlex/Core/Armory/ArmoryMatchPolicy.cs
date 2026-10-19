using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Armory;

public static class ArmoryMatchPolicy
{
	public static ArmoryMatch Pick(IReadOnlyList<ArmoryCharacter> results, string name, JobClass jobClass, int? knownServerId, int? localServerId)
	{
		List<ArmoryCharacter> list = (from c in results
			where string.Equals(c.Name, name, StringComparison.Ordinal)
			where jobClass == JobClass.Unknown || c.JobClass == jobClass
			group c by c.CharacterId into g
			select g.First()).OrderByDescending((ArmoryCharacter c) =>
		{
			if (knownServerId.HasValue)
			{
				int valueOrDefault2 = knownServerId.GetValueOrDefault();
				return c.ServerId == valueOrDefault2;
			}
			return false;
		}).ThenByDescending((ArmoryCharacter c) =>
		{
			if (localServerId.HasValue)
			{
				int valueOrDefault2 = localServerId.GetValueOrDefault();
				return c.ServerId == valueOrDefault2;
			}
			return false;
		}).ThenByDescending((ArmoryCharacter c) => c.Level)
			.ThenBy((ArmoryCharacter c) => c.ServerId)
			.ToList();
		if (list.Count == 0)
		{
			return new ArmoryMatch(null, list);
		}
		if (knownServerId.HasValue)
		{
			int valueOrDefault = knownServerId.GetValueOrDefault();
			if (list[0].ServerId != valueOrDefault)
			{
				return new ArmoryMatch(null, list);
			}
		}
		return new ArmoryMatch(list[0], list);
	}
}
