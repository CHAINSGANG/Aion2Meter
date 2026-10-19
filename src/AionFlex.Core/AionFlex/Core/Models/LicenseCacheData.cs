using System;

namespace AionFlex.Core.Models;

public class LicenseCacheData
{
	public string LicenseKey { get; set; } = string.Empty;

	public string HardwareFingerprint { get; set; } = string.Empty;

	public string Email { get; set; } = string.Empty;

	public DateTime ExpiresAt { get; set; }

	public DateTime LastValidatedAt { get; set; }

	public bool IsTrial { get; set; }
}
