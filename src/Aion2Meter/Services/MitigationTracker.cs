using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

/// <summary>
/// Tracks the same incoming mitigation percentage shown by AionFlex:
/// attacks that were blocked, endured, evaded or resisted divided by all
/// non-periodic incoming attack attempts.
/// </summary>
internal sealed class MitigationTracker
{
    private sealed class IncomingCounter
    {
        public int LandedHits;
        public int PlainHits;
        public int AvoidedHits;
    }

    private readonly record struct AvoidKey(
        long Timestamp,
        int ActorId,
        int TargetId,
        int SkillCode,
        int EventCounter,
        DefenseOutcome Outcome);

    private readonly object _gate = new();
    private readonly Dictionary<int, IncomingCounter> _incoming = [];
    private readonly HashSet<AvoidKey> _seenAvoids = [];

    public void Record(DamageEvent damageEvent)
    {
        if (damageEvent.ActorId == damageEvent.TargetId ||
            damageEvent.TargetId <= 0 ||
            damageEvent.IsHeal ||
            damageEvent.IsPassive ||
            damageEvent.IsDoT ||
            damageEvent.Damage < 0)
        {
            return;
        }

        bool endured = damageEvent.SpecialFlags.HasFlag(SpecialDamageFlags.Endure);

        lock (_gate)
        {
            IncomingCounter counter = GetOrCreate(damageEvent.TargetId);
            counter.LandedHits++;
            if (!damageEvent.IsBlocked && !endured)
            {
                counter.PlainHits++;
            }
        }
    }

    public void Record(DefenseEvent defenseEvent)
    {
        if (defenseEvent.TargetId <= 0 ||
            defenseEvent.Outcome is not (DefenseOutcome.Evade or DefenseOutcome.Resist))
        {
            return;
        }

        var key = new AvoidKey(
            defenseEvent.Timestamp,
            defenseEvent.ActorId,
            defenseEvent.TargetId,
            defenseEvent.SkillCode,
            defenseEvent.EventCounter,
            defenseEvent.Outcome);

        lock (_gate)
        {
            if (!_seenAvoids.Add(key))
            {
                return;
            }

            GetOrCreate(defenseEvent.TargetId).AvoidedHits++;
        }
    }

    public double GetRate(int playerId)
    {
        lock (_gate)
        {
            if (!_incoming.TryGetValue(playerId, out IncomingCounter? counter))
            {
                return 0;
            }

            int totalHits = counter.LandedHits + counter.AvoidedHits;
            if (totalHits <= 0)
            {
                return 0;
            }

            return Math.Clamp((totalHits - counter.PlainHits) * 100d / totalHits, 0d, 100d);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _incoming.Clear();
            _seenAvoids.Clear();
        }
    }

    private IncomingCounter GetOrCreate(int targetId)
    {
        if (!_incoming.TryGetValue(targetId, out IncomingCounter? counter))
        {
            counter = new IncomingCounter();
            _incoming[targetId] = counter;
        }

        return counter;
    }
}
