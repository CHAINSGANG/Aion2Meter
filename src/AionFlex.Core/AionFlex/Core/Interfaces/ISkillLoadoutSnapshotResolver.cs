using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface ISkillLoadoutSnapshotResolver
{
	ResolvedSkillLoadout? Resolve(SkillLoadoutSnapshot? loadout, Language language);
}
