using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

internal static class TargetDebuffSelection
{
    public static IReadOnlyList<MeterDebuffSnapshot> Project(
        IReadOnlyList<ActiveDebuffWindow> active,
        int? localPlayerId,
        bool includeOtherPlayers,
        long nowMs,
        Func<int, int?> getSummoner,
        Func<int, bool> isKnownPlayer,
        Func<int, SkillMetadata?> getMetadata,
        Func<int, string> getSkillName,
        Func<int, string?> getPlayerName)
    {
        return active
            .Select(effect => new
            {
                Effect = effect,
                OwnerId = getSummoner(effect.CasterId) ?? effect.CasterId,
                SkillCode = effect.SourceSkillId / 10_000 * 10_000,
                Metadata = getMetadata(effect.SourceSkillId)
            })
            .Where(item => item.OwnerId > 0 &&
                (item.OwnerId == localPlayerId || includeOtherPlayers && isKnownPlayer(item.OwnerId)) &&
                item.SkillCode > 0 && item.Metadata?.IsDebuff == true &&
                item.Effect.DurationMs >= 750)
            .GroupBy(item => (item.OwnerId, item.SkillCode))
            .Select(group =>
            {
                var longest = group.OrderByDescending(item => item.Effect.EndMs).First();
                int stacks = group
                    .GroupBy(item => item.Effect.AbnormalId)
                    .Select(items => items.Count())
                    .DefaultIfEmpty(1)
                    .Max();
                return new MeterDebuffSnapshot(
                    group.Key.SkillCode,
                    getSkillName(group.Key.SkillCode),
                    Math.Max(0, longest.Effect.EndMs - nowMs),
                    longest.Effect.DurationMs,
                    stacks,
                    group.Key.OwnerId,
                    getPlayerName(group.Key.OwnerId),
                    group.Key.OwnerId != localPlayerId);
            })
            .OrderBy(effect => effect.RemainingMs)
            .ThenBy(effect => effect.SkillCode)
            .ThenBy(effect => effect.OwnerId)
            .Take(includeOtherPlayers ? 12 : 8)
            .ToArray();
    }
}
