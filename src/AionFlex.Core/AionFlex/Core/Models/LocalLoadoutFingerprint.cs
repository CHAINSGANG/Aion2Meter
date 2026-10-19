using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record LocalLoadoutFingerprint(IReadOnlyList<int>? SpecCodes, IReadOnlyList<int>? SpecStigmaCodes, IReadOnlyList<string>? ArcanaSetIds, long? ObservedAtMs)
{
	public bool IsEmpty
	{
		get
		{
			IReadOnlyList<int> specCodes = SpecCodes;
			if (specCodes == null || specCodes.Count <= 0)
			{
				specCodes = SpecStigmaCodes;
				if (specCodes == null || specCodes.Count <= 0)
				{
					IReadOnlyList<string> arcanaSetIds = ArcanaSetIds;
					return arcanaSetIds == null || arcanaSetIds.Count <= 0;
				}
			}
			return false;
		}
	}
}
