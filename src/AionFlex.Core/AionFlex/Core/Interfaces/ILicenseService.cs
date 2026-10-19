using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface ILicenseService
{
	bool HasCachedLicense();

	LicenseCacheData? GetCachedLicenseInfo();
}
