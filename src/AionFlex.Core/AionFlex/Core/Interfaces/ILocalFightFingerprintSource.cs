using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface ILocalFightFingerprintSource
{
	LocalLoadoutFingerprint? CaptureLoadout();

	uint? GetCharacterId(int localEntityId);
}
