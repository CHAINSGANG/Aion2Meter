using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Overlay;

public static class OverlayRowColorPolicy
{
	public static readonly IReadOnlyDictionary<JobClass, uint> ClassColorDefaults = new Dictionary<JobClass, uint>
	{
		[JobClass.Gladiator] = 4286698987u,
		[JobClass.Templar] = 4286028270u,
		[JobClass.Ranger] = 4284790162u,
		[JobClass.Assassin] = 4288206736u,
		[JobClass.Sorcerer] = 4289693416u,
		[JobClass.Cleric] = 4293579390u,
		[JobClass.Elementalist] = 4290076343u,
		[JobClass.Chanter] = 4293108826u,
		[JobClass.Fighter] = 4291913318u
	};

	public static uint? ResolveRowColor(OverlayRowColorMode mode, JobClass job, uint singleArgb, IReadOnlyDictionary<JobClass, uint>? classOverrides = null)
	{
		switch (mode)
		{
		case OverlayRowColorMode.Single:
			return singleArgb;
		case OverlayRowColorMode.Class:
		{
			if (job == JobClass.Unknown)
			{
				return null;
			}
			if (classOverrides != null && classOverrides.TryGetValue(job, out var value))
			{
				return value;
			}
			if (!ClassColorDefaults.TryGetValue(job, out var value2))
			{
				return null;
			}
			return value2;
		}
		default:
			return null;
		}
	}
}
