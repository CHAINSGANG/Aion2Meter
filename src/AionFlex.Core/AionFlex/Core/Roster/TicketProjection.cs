using System;
using AionFlex.Core.ContentsTickets;

namespace AionFlex.Core.Roster;

public static class TicketProjection
{
	private const int MaxSteps = 4096;

	public static TicketNow Project(int basic, int bonus, DateTime readAtUtc, ContentsTicketInfo info, TimeSpan? serverOffset, DateTime nowUtc)
	{
		basic = Math.Max(0, basic);
		bonus = Math.Max(0, bonus);
		int num = Math.Max(0, info.Max);
		if (serverOffset.HasValue)
		{
			TimeSpan valueOrDefault = serverOffset.GetValueOrDefault();
			if (!info.IsTime && info.Refills.Count != 0)
			{
				int num2 = basic;
				DateTime nowUtc2 = readAtUtc;
				for (int i = 0; i < 4096; i++)
				{
					if (num != 0 && num2 >= num)
					{
						break;
					}
					(DateTime, int)? tuple = ContentsTicketClock.NextOf(info, valueOrDefault, nowUtc2);
					if (!tuple.HasValue)
					{
						break;
					}
					(DateTime, int) valueOrDefault2 = tuple.GetValueOrDefault();
					if (valueOrDefault2.Item1 > nowUtc)
					{
						break;
					}
					num2 = ((num > 0) ? Math.Min(num, num2 + valueOrDefault2.Item2) : (num2 + valueOrDefault2.Item2));
					nowUtc2 = valueOrDefault2.Item1;
				}
				(DateTime, int)? tuple2 = ContentsTicketClock.NextOf(info, valueOrDefault, nowUtc);
				return new TicketNow(Math.Max(basic, num2), bonus, num, tuple2?.Item1, tuple2?.Item2 ?? 0, Projected: true);
			}
		}
		return new TicketNow(basic, bonus, num, null, 0, Projected: false);
	}
}
