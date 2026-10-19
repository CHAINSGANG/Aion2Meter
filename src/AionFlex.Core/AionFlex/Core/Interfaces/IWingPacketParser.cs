using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IWingPacketParser : IFeaturePacketParser
{
	event EventHandler<WingSnapshot>? WingRosterDiscovered;

	event EventHandler<(string CharacterName, int WingId)>? EquippedWingForCharacterDiscovered;

	event EventHandler<int>? EquippedWingForLocalPlayerDiscovered;

	void SetValidWingIds(IReadOnlySet<int> wingIds);
}
