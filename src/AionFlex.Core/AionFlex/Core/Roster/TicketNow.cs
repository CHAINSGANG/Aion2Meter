using System;

namespace AionFlex.Core.Roster;

public readonly record struct TicketNow(int Basic, int Bonus, int Cap, DateTime? NextRefillUtc, int NextRefillCount, bool Projected)
{
	public bool IsFull
	{
		get
		{
			if (Cap > 0)
			{
				return Basic >= Cap;
			}
			return false;
		}
	}

	public int Available => Basic + Bonus;
}
