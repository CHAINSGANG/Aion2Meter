using System;

namespace AionFlex.Core.Capture;

public sealed record DevDataBudgets(long CapturesBytes, long LogsBytes, long DebugDumpsBytes, int KeepPreviousSessions, TimeSpan EmptyMinAge)
{
	public static DevDataBudgets Default => new DevDataBudgets(5368709120L, 2147483648L, 1073741824L, 2, TimeSpan.FromMinutes(10.0));
}
