using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record EntitlementSnapshot(string RawJwt, string LicenseId, string Tier, IReadOnlyList<string> Features, string Hwid, DateTime IssuedAt, DateTime ExpiresAt)
{
	public bool ProBoundElsewhere { get; init; }

	public static EntitlementSnapshot Free(string hwid)
	{
		return new EntitlementSnapshot(string.Empty, string.Empty, "free", Array.Empty<string>(), hwid, DateTime.UtcNow, DateTime.UtcNow);
	}

	public static EntitlementSnapshot ProBoundElsewhereSnapshot(string hwid)
	{
		return Free(hwid)with
		{
			ProBoundElsewhere = true
		};
	}
}
