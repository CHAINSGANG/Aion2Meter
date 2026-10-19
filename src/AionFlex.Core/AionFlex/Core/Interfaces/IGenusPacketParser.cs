using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IGenusPacketParser : IFeaturePacketParser
{
	event EventHandler<GenusInsightSnapshot>? GenusInsightDiscovered;

	event EventHandler<(uint CharacterId, uint AccountDiscriminator)>? CharacterIdentityDiscovered;
}
