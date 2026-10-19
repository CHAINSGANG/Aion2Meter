using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

internal sealed record ActiveDebuffWindow(
    int TargetId,
    int Slot,
    int AbnormalId,
    int CasterId,
    int SourceSkillId,
    long StartMs,
    long EndMs,
    long DurationMs);

/// <summary>
/// Keeps the live abnormal-effect slots reported by the game. Applying or
/// refreshing replaces a slot, removing closes it immediately, and timed
/// effects expire locally if a removal packet is missed.
/// </summary>
internal sealed class DebuffTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<(int TargetId, int Slot), ActiveDebuffWindow> _open = [];

    public void Record(AbnormalEvent abnormalEvent)
    {
        if (abnormalEvent.TargetId <= 0 || abnormalEvent.Slot < 0)
        {
            return;
        }

        var key = (abnormalEvent.TargetId, abnormalEvent.Slot);
        lock (_gate)
        {
            if (abnormalEvent.IsRemove)
            {
                _open.Remove(key);
                return;
            }

            if (abnormalEvent.DurationMs is 0 or uint.MaxValue || abnormalEvent.SourceSkillId <= 0)
            {
                _open.Remove(key);
                return;
            }

            long durationMs = abnormalEvent.DurationMs;
            long startMs = abnormalEvent.Timestamp > 0
                ? abnormalEvent.Timestamp
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _open[key] = new ActiveDebuffWindow(
                abnormalEvent.TargetId,
                abnormalEvent.Slot,
                abnormalEvent.AbnormalId,
                abnormalEvent.CasterId,
                abnormalEvent.SourceSkillId,
                startMs,
                startMs + durationMs,
                durationMs);
        }
    }

    public IReadOnlyList<ActiveDebuffWindow> GetActive(int targetId, long nowMs)
    {
        lock (_gate)
        {
            foreach ((int TargetId, int Slot) key in _open
                         .Where(pair => pair.Value.EndMs <= nowMs)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _open.Remove(key);
            }

            return _open.Values
                .Where(effect => effect.TargetId == targetId && effect.EndMs > nowMs)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _open.Clear();
        }
    }
}
