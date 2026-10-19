using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IDaevanionSnapshotResolver
{
	ResolvedDaevanionSnapshot? Resolve(DaevanionSnapshot? unlocked, DaevanionSnapshot? activated, Language language);
}
