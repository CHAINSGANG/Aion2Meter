using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IFeatureGate
{
	string CurrentTier { get; }

	bool IsProBoundElsewhere { get; }

	event EventHandler<EntitlementSnapshot>? TierChanged;

	bool IsEnabled(Feature feature);
}
