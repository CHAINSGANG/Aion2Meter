using System;
using System.Collections.Generic;
using AionFlex.Core.Combat;

namespace AionFlex.Core.Models;

public sealed class TargetStatistics
{
	private long _absorbedTotal;

	private long _absorbedAtLatestTimestamp;

	private long _latestTimestamp = long.MinValue;

	private long _lastClassSkillHealMs = long.MinValue;

	private Dictionary<int, long>? _lastHealMsByHealer;

	private readonly HashSet<DamageEvent> _processedEvents = new HashSet<DamageEvent>(ReferenceEqualityComparer.Instance);

	public int TargetId { get; init; }

	public long TotalDamageReceived { get; private set; }

	public long FirstDamageTime { get; private set; }

	public long LastDamageTime { get; private set; }

	public long DamageAbsorbedWhileAlive => _absorbedTotal - _absorbedAtLatestTimestamp;

	public bool ReceivedPlayerHeal(Func<int, bool> isPlayerActor, long sinceMs = long.MinValue)
	{
		if (_lastClassSkillHealMs != long.MinValue && _lastClassSkillHealMs >= sinceMs)
		{
			return true;
		}
		if (_lastHealMsByHealer == null)
		{
			return false;
		}
		foreach (var (arg, num3) in _lastHealMsByHealer)
		{
			if (num3 >= sinceMs && isPlayerActor(arg))
			{
				return true;
			}
		}
		return false;
	}

	public TargetStatistics(int targetId, long initialTimestamp)
	{
		TargetId = targetId;
		FirstDamageTime = initialTimestamp;
		LastDamageTime = initialTimestamp;
	}

	public bool ProcessDamageEvent(DamageEvent damageEvent)
	{
		if (_processedEvents.Contains(damageEvent))
		{
			return false;
		}
		TotalDamageReceived += damageEvent.TotalDamage;
		long timestamp = damageEvent.Timestamp;
		if (timestamp < FirstDamageTime)
		{
			FirstDamageTime = timestamp;
		}
		else if (timestamp > LastDamageTime)
		{
			LastDamageTime = timestamp;
		}
		if (damageEvent.IsHeal)
		{
			if (BossLabelPolicy.ProvesFriendlyTarget(damageEvent.SkillCode, isHeal: true, actorIsConfirmedPlayer: false, actorIsLocalPlayer: false))
			{
				_lastClassSkillHealMs = Math.Max(_lastClassSkillHealMs, timestamp);
			}
			else
			{
				Dictionary<int, long> dictionary = _lastHealMsByHealer ?? (_lastHealMsByHealer = new Dictionary<int, long>());
				dictionary[damageEvent.ActorId] = (dictionary.TryGetValue(damageEvent.ActorId, out var value) ? Math.Max(value, timestamp) : timestamp);
			}
		}
		if (PartyMemberEvidencePolicy.CountsAsAbsorbedDamage(damageEvent.IsHeal, damageEvent.ActorId, damageEvent.TargetId))
		{
			_absorbedTotal += damageEvent.TotalDamage;
			if (timestamp > _latestTimestamp)
			{
				_latestTimestamp = timestamp;
				_absorbedAtLatestTimestamp = damageEvent.TotalDamage;
			}
			else if (timestamp == _latestTimestamp)
			{
				_absorbedAtLatestTimestamp += damageEvent.TotalDamage;
			}
		}
		_processedEvents.Add(damageEvent);
		return true;
	}

	public void PruneProcessedTo(IReadOnlyCollection<DamageEvent> live)
	{
		if (_processedEvents.Count > live.Count)
		{
			_processedEvents.IntersectWith(live);
		}
	}

	public long GetBattleTimeMs()
	{
		return LastDamageTime - FirstDamageTime;
	}

	public int GetEventCount()
	{
		return _processedEvents.Count;
	}
}
