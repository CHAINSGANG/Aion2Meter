using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface ITitlesPacketParser : IFeaturePacketParser
{
	event EventHandler<EquippedTitlesSnapshot>? EquippedTitlesDiscovered;

	void SetValidTitleIds(IReadOnlySet<int> titleIds);
}
