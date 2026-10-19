using System.Collections.Generic;

namespace AionFlex.Core.ContentsTickets;

public sealed record ContentsTicketInfo(uint Id, string Internal, string En, string? Ko, string? Zh, bool IsTime, int Max, int SubscribeMax, bool ShowMax, string Unlock, IReadOnlyList<ContentsTicketRefill> Refills)
{
	public string Name(string language)
	{
		if (!(language == "ko"))
		{
			if (language == "zh" && !string.IsNullOrEmpty(Zh))
			{
				return Zh;
			}
		}
		else if (!string.IsNullOrEmpty(Ko))
		{
			return Ko;
		}
		return En;
	}
}
