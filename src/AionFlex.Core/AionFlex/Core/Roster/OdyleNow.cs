using System;

namespace AionFlex.Core.Roster;

public readonly record struct OdyleNow(int Basic, int Bonus, int Cap, bool IsFull, DateTime? NextRefillUtc, DateTime? FullAtUtc, bool Projected)
{
	public int Available => Basic + Bonus;
}
