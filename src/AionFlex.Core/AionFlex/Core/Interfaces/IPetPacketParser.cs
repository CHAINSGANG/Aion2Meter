using System;
using System.Collections.Generic;

namespace AionFlex.Core.Interfaces;

public interface IPetPacketParser : IFeaturePacketParser
{
	event EventHandler<IReadOnlyList<(int PetId, int Level)>>? PetRosterDiscovered;
}
