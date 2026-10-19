using System;
using System.Linq;
using AionFlex.Core.ContentsTickets;

namespace AionFlex.Core.Roster;

public static class OdyleProjection
{
	public static int CapFor(ContentsTicketInfo info)
	{
		if (info.SubscribeMax <= info.Max)
		{
			return info.Max;
		}
		return info.SubscribeMax;
	}

	public static OdyleNow Project(int basic, int bonus, DateTime readAtUtc, ContentsTicketInfo info, TimeSpan? serverOffset, int cap, DateTime nowUtc)
	{
		basic = Math.Max(0, basic);
		bonus = Math.Max(0, bonus);
		ContentsTicketRefill contentsTicketRefill = info.Refills.FirstOrDefault((ContentsTicketRefill r) => r.IsEvery && r.Count > 0);
		if (serverOffset.HasValue)
		{
			TimeSpan valueOrDefault = serverOffset.GetValueOrDefault();
			if ((object)contentsTicketRefill != null && cap > 0)
			{
				TimeSpan timeSpan = TimeSpan.FromSeconds(contentsTicketRefill.EverySeconds);
				int num = basic;
				if (basic < cap)
				{
					DateTime dateTime = ContentsTicketClock.Next(contentsTicketRefill, valueOrDefault, readAtUtc);
					if (dateTime <= nowUtc)
					{
						long num2 = 1 + (nowUtc - dateTime).Ticks / timeSpan.Ticks;
						num = (int)Math.Min(cap, basic + num2 * contentsTicketRefill.Count);
					}
				}
				DateTime dateTime2 = ContentsTicketClock.Next(contentsTicketRefill, valueOrDefault, nowUtc);
				bool flag = num >= cap;
				DateTime? fullAtUtc = null;
				if (!flag)
				{
					int num3 = (cap - num + contentsTicketRefill.Count - 1) / contentsTicketRefill.Count;
					fullAtUtc = dateTime2 + TimeSpan.FromTicks(timeSpan.Ticks * (num3 - 1));
				}
				return new OdyleNow(num, bonus, cap, flag, dateTime2, fullAtUtc, Projected: true);
			}
		}
		return new OdyleNow(basic, bonus, cap, cap > 0 && basic >= cap, null, null, Projected: false);
	}
}
