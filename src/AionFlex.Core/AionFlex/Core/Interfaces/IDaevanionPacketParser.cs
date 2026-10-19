using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IDaevanionPacketParser : IFeaturePacketParser
{
	event EventHandler<DaevanionSnapshot>? DaevanionDiscovered;
}
