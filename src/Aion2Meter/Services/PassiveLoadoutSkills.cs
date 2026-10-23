using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

public sealed record MeterPassiveSkillSnapshot(int SkillCode, string Name, int Level, bool IsEquipped);

internal static class PassiveLoadoutSkills
{
    public static IReadOnlyList<MeterPassiveSkillSnapshot> FromLoadout(
        SkillLoadoutSnapshot loadout,
        Func<int, SkillMetadata?> getMetadata,
        Func<int, string> getName)
    {
        // A roster level proves that a passive is learned. Only an equipped entry
        // proves that it was selected; keep these states distinct in the UI.
        var equipped = loadout.Equipped
            .Where(entry => entry.BaseCode > 0 && entry.Level > 0 && getMetadata(entry.BaseCode)?.IsPassive == true)
            .GroupBy(entry => entry.BaseCode)
            .ToDictionary(group => group.Key, group => group.Max(entry => entry.Level));

        return loadout.Roster
            .Where(entry => entry.Code > 0 && entry.Level > 0 && getMetadata(entry.Code)?.IsPassive == true)
            .Select(entry => (Code: entry.Code, entry.Level))
            .Concat(equipped.Select(entry => (Code: entry.Key, Level: entry.Value)))
            .GroupBy(entry => entry.Code)
            .OrderBy(group => group.Key)
            .Select(group => new MeterPassiveSkillSnapshot(
                group.Key,
                getName(group.Key),
                group.Max(entry => entry.Level),
                equipped.ContainsKey(group.Key)))
            .ToArray();
    }
}
