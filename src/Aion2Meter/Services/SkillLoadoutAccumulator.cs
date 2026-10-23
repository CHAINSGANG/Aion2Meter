using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

internal static class SkillLoadoutAccumulator
{
    // A compressed bundle may contain the whole loadout or only changed skills.
    // Absence from one bundle is not proof that another skill was unequipped.
    public static IReadOnlyList<SkillRosterEntry> MergeRoster(
        IReadOnlyList<SkillRosterEntry> current,
        IReadOnlyList<SkillRosterEntry> incoming) =>
        current.Concat(incoming)
            .Where(entry => entry.Code > 0)
            .GroupBy(entry => entry.Code)
            .OrderBy(group => group.Key)
            .Select(group => group.Last())
            .ToArray();

    public static IReadOnlyList<SkillEquippedEntry> MergeEquipped(
        IReadOnlyList<SkillEquippedEntry> current,
        IReadOnlyList<SkillEquippedEntry> incoming) =>
        current.Concat(incoming)
            .Where(entry => entry.BaseCode > 0)
            .GroupBy(entry => entry.BaseCode)
            .OrderBy(group => group.Key)
            .Select(group => group.Last())
            .ToArray();
}
