using System.Collections.Generic;
using AionFlex.Core.Interfaces;

namespace AionFlex.Application.DpsCalculation;

public static class SummonOwnerResolver
{
	public const int MaxHops = 16;

	public static int Resolve(IDataRepository repository, int actorId, int? blockedEntityId = null, long? timestampMs = null)
	{
		if (blockedEntityId.HasValue)
		{
			int valueOrDefault = blockedEntityId.GetValueOrDefault();
			if (actorId == valueOrDefault)
			{
				return actorId;
			}
		}
		int? num;
		if (timestampMs.HasValue)
		{
			long valueOrDefault2 = timestampMs.GetValueOrDefault();
			num = repository.GetSummonerAt(actorId, valueOrDefault2);
		}
		else
		{
			num = repository.GetSummoner(actorId);
		}
		int? num2 = num;
		if (num2.HasValue)
		{
			int valueOrDefault3 = num2.GetValueOrDefault();
			if (valueOrDefault3 > 0)
			{
				int num3 = actorId;
				int? num4 = num2;
				int i = 0;
				HashSet<int> hashSet = new HashSet<int>();
				for (; i < 16; i++)
				{
					if (!hashSet.Add(num3))
					{
						break;
					}
					if (i > 0)
					{
						num4 = repository.GetSummoner(num3);
					}
					if (!num4.HasValue || num4.Value <= 0)
					{
						break;
					}
					num3 = num4.Value;
					if (blockedEntityId.HasValue)
					{
						int valueOrDefault4 = blockedEntityId.GetValueOrDefault();
						if (num3 == valueOrDefault4)
						{
							return actorId;
						}
					}
				}
				bool flag = repository.IsConfirmedPlayer(num3) || repository.GetLocalPlayerEntityId() == num3;
				if (!((num3 != actorId) & flag))
				{
					return actorId;
				}
				return num3;
			}
		}
		return actorId;
	}
}
