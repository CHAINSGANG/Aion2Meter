using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IWingsSnapshotResolver
{
	ResolvedWingsSnapshot? Resolve(WingSnapshot? snapshot, int? equippedWingId, Language language);
}
