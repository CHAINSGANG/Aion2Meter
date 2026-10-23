using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

public sealed record MeterSkillSpecialtySnapshot(
    int SkillCode,
    string SkillName,
    int Level,
    IReadOnlyList<int> SelectedOptions);

internal static class SkillSpecialtySelection
{
    public static IReadOnlyList<MeterSkillSpecialtySnapshot> FromLoadout(
        SkillLoadoutSnapshot loadout,
        Func<int, SkillMetadata?> getMetadata,
        Func<int, string> getName) =>
        loadout.Equipped
            .Where(entry => entry.BaseCode > 0 && entry.Specs is { Count: > 0 })
            .Where(entry => getMetadata(entry.BaseCode) is { IsPassive: false, IsStigma: false })
            .GroupBy(entry => entry.BaseCode)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                SkillEquippedEntry latest = group.Last();
                int[] options = latest.Specs
                    .Where(option => option is >= 1 and <= 5)
                    .Distinct()
                    .OrderBy(option => option)
                    .ToArray();
                return new MeterSkillSpecialtySnapshot(
                    group.Key,
                    getName(group.Key),
                    latest.Level,
                    options);
            })
            .Where(entry => entry.SelectedOptions.Count > 0)
            .ToArray();
}
