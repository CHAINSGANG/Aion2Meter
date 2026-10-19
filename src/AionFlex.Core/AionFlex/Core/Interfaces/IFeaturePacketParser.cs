using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IFeaturePacketParser
{
	void ProcessPayload(CapturedPayload payload);
}
