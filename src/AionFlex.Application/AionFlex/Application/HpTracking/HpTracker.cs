using System;
using System.Collections.Concurrent;
using AionFlex.Core.Combat;
using AionFlex.Core.Models;

namespace AionFlex.Application.HpTracking;

/// <summary>
/// Keeps HP readings from the spawn and wire-update lanes tied to one entity life.
/// The game does not include a reliable maximum in every HP update, so the last
/// current value alone is not sufficient for a stable target bar.
/// </summary>
public sealed class HpTracker
{
    private readonly ConcurrentDictionary<int, ActorHpData> _actors = new();
    private uint _wipeMinEntityMaxHp = 500_000;

    public event EventHandler<BossHpResetEvent>? BossHpReset;

    public event Action<string>? Diagnostic;

    public void SetWipeMinEntityMaxHp(uint threshold) => _wipeMinEntityMaxHp = threshold;

    public void ProcessHpUpdate(HpUpdateEvent hpEvent)
    {
        if (hpEvent.ActorId <= 0)
        {
            return;
        }

        ActorHpData data = _actors.GetOrAdd(hpEvent.ActorId, id => new ActorHpData { ActorId = id });
        BossHpResetEvent? reset = null;

        lock (data)
        {
            // UDP lanes may deliver an older reading after a newer one. Let the
            // framed spawn packet replace state, but never rewind ordinary updates.
            if (data.LastHpTimestamp > hpEvent.Timestamp && hpEvent.Source != HpReadingSource.FramedSpawn)
            {
                Diagnostic?.Invoke($"REJECT stale actor={hpEvent.ActorId} source={hpEvent.Source} packetTs={hpEvent.Timestamp} stateTs={data.LastHpTimestamp}");
                return;
            }

            if (data.FirstHp == 0)
            {
                data.FirstHp = hpEvent.CurrentHp;
                data.FirstHpTimestamp = hpEvent.Timestamp;
                data.AttemptStartMs = hpEvent.Timestamp;
            }

            uint? previousWireHp = null;
            bool trustedFullSpawn = false;

            if (hpEvent.Source == HpReadingSource.FramedSpawn && hpEvent.SpawnNpcId is > 0)
            {
                if (data.LastSpawnNpcId != 0 && data.LastSpawnNpcId != hpEvent.SpawnNpcId.Value)
                {
                    data.LastWireHp = null;
                }

                data.LastSpawnNpcId = hpEvent.SpawnNpcId.Value;
            }
            else if (hpEvent.Source == HpReadingSource.HpUpdate008D)
            {
                previousWireHp = data.LastWireHp;
                trustedFullSpawn = hpEvent.FramedSpawnMaxHp is > 0 &&
                                   hpEvent.CurrentHp == hpEvent.FramedSpawnMaxHp.Value;
            }

            // A malformed 008D packet can decode into a huge number. The original
            // meter rejects it once enough consistent readings have established HP.
            if (!trustedFullSpawn &&
                data.UpdateCount >= 5 &&
                data.MaxHp > 0 &&
                hpEvent.CurrentHp > (ulong)data.MaxHp * 2)
            {
                Diagnostic?.Invoke($"REJECT spike actor={hpEvent.ActorId} hp={hpEvent.CurrentHp} knownMax={data.MaxHp} updates={data.UpdateCount}");
                return;
            }

            bool isBackToFull = BossHpResetPolicy.IsBackToFull(
                hpEvent.Source,
                hpEvent.CurrentHp,
                previousWireHp,
                hpEvent.FramedSpawnMaxHp.GetValueOrDefault(),
                data.ReachedZeroHp,
                _wipeMinEntityMaxHp);

            if (hpEvent.Source == HpReadingSource.HpUpdate008D)
            {
                data.LastWireHp = hpEvent.CurrentHp;
            }

            bool aboveKnownMaximum = data.MaxHp > 0 && hpEvent.CurrentHp > data.MaxHp;
            data.LastHp = hpEvent.CurrentHp;
            data.LastHpTimestamp = hpEvent.Timestamp;
            data.UpdateCount++;

            if (hpEvent.CurrentHp == 0)
            {
                if (!data.ReachedZeroHp)
                {
                    data.ZeroHpTimestamp = hpEvent.Timestamp;
                }

                data.ReachedZeroHp = true;
            }
            else if (!data.ReachedZeroHp &&
                     (data.MinAliveHp == 0 || hpEvent.CurrentHp < data.MinAliveHp || aboveKnownMaximum))
            {
                data.MinAliveHp = hpEvent.CurrentHp;
                data.MinAliveHpTimestamp = hpEvent.Timestamp;
            }

            if (hpEvent.MaxHp is > 0)
            {
                data.ServerMaxHp = Math.Max(data.ServerMaxHp, hpEvent.MaxHp.Value);
                data.MaxHp = Math.Max(data.MaxHp, hpEvent.MaxHp.Value);
            }

            data.MaxHp = Math.Max(data.MaxHp, hpEvent.CurrentHp);

            if (isBackToFull && previousWireHp.HasValue && hpEvent.FramedSpawnMaxHp.HasValue)
            {
                data.FirstHp = hpEvent.CurrentHp;
                data.FirstHpTimestamp = hpEvent.Timestamp;
                data.UpdateCount = 1;
                data.MinAliveHp = hpEvent.CurrentHp;
                data.MinAliveHpTimestamp = hpEvent.Timestamp;
                data.AttemptStartMs = hpEvent.Timestamp;
                reset = new BossHpResetEvent(
                    hpEvent.ActorId,
                    hpEvent.Timestamp,
                    previousWireHp.Value,
                    hpEvent.CurrentHp,
                    hpEvent.FramedSpawnMaxHp.Value);
            }

            Diagnostic?.Invoke($"ACCEPT actor={hpEvent.ActorId} source={hpEvent.Source} hp={data.LastHp}/{data.MaxHp} serverMax={data.ServerMaxHp} updates={data.UpdateCount} zero={data.ReachedZeroHp}");
        }

        if (reset.HasValue)
        {
            BossHpReset?.Invoke(this, reset.Value);
        }
    }

    public void SetMaxHp(int actorId, uint maxHp)
    {
        if (actorId <= 0 || maxHp == 0)
        {
            return;
        }

        ActorHpData data = _actors.GetOrAdd(actorId, id => new ActorHpData { ActorId = id });
        lock (data)
        {
            data.MaxHp = Math.Max(data.MaxHp, maxHp);
            data.ServerMaxHp = Math.Max(data.ServerMaxHp, maxHp);
            Diagnostic?.Invoke($"MAX actor={actorId} catalogMax={maxHp} effectiveMax={data.MaxHp}");
        }
    }

    public ActorHpData? GetActorHpData(int actorId) =>
        _actors.TryGetValue(actorId, out ActorHpData? data) ? data : null;

    public long? TryGetTrustedMaxHp(int actorId, long firstDamageTimestampMs)
    {
        ActorHpData? data = GetActorHpData(actorId);
        if (data is null)
        {
            return null;
        }

        lock (data)
        {
            if (data.ServerMaxHp == 0 || data.MaxHp == 0)
            {
                return null;
            }

            if (data.MaxHp <= data.ServerMaxHp)
            {
                return data.ServerMaxHp;
            }

            return firstDamageTimestampMs > 0 && data.FirstHpTimestamp <= firstDamageTimestampMs
                ? data.MaxHp
                : null;
        }
    }

    public HpProgress? TryGetHpProgress(int actorId)
    {
        ActorHpData? data = GetActorHpData(actorId);
        if (data is null)
        {
            return null;
        }

        lock (data)
        {
            return new HpProgress(
                data.MaxHp,
                data.MinAliveHp,
                data.MinAliveHpTimestamp,
                data.ReachedZeroHp,
                data.AttemptStartMs);
        }
    }

    public void ResetActor(int actorId)
    {
        bool removed = _actors.TryRemove(actorId, out _);
        Diagnostic?.Invoke($"RESET actor={actorId} existed={removed}");
    }

    public void ResetAll()
    {
        int count = _actors.Count;
        _actors.Clear();
        Diagnostic?.Invoke($"RESET-ALL actors={count}");
    }
}
