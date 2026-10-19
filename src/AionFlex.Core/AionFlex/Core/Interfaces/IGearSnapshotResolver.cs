using System.Collections.Generic;
using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IGearSnapshotResolver
{
	IReadOnlyList<ResolvedGearSlot> Resolve(IReadOnlyDictionary<int, GearItem> localGear, Language language);
}
