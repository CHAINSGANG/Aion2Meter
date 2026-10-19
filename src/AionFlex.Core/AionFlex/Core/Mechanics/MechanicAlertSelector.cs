using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Mechanics;

public static class MechanicAlertSelector
{
	public static BossMechanics? Select(IReadOnlyList<BossMechanics> bosses, int bossNpcId)
	{
		if (bossNpcId <= 0)
		{
			return null;
		}
		foreach (BossMechanics boss in bosses)
		{
			if (boss.NpcIds.Contains(bossNpcId) && boss.Mechanics.Any((BossMechanic m) => m.LiveTrigger != null))
			{
				return boss;
			}
		}
		return null;
	}
}
