using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IPlayerStatsPacketParser : IFeaturePacketParser
{
	event EventHandler<PlayerStatsSnapshot>? PlayerStatsDiscovered;
}
