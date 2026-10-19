using System;
using System.Collections.Generic;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;

namespace AionFlex.Core.Interfaces;

public interface IGearPacketParser : IFeaturePacketParser
{
	event EventHandler<IReadOnlyList<GearLoginBundleParser.BundleItem>>? LoginGearBundleDecoded;

	event EventHandler<(int ItemId, IReadOnlyList<GearSubStat> SubStats)>? GearDetailDecoded;

	event EventHandler<GearChange>? GearChangeDetected;

	event EventHandler<(string CharacterName, IReadOnlyList<int> ItemIds)>? EquippedGearForCharacterDiscovered;

	event EventHandler<IReadOnlyList<int>>? LobbyGearListDiscovered;

	void SetValidItemIds(IReadOnlySet<int> itemIds);
}
