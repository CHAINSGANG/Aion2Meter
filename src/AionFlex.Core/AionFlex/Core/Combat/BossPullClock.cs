using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Combat;

public sealed class BossPullClock
{
	private sealed class Pull
	{
		public long AttemptStartMs;

		public long LastActivityMs;

		public long? FirstHitMs;

		public long Damage;

		public long? EnrageAtMs;

		public readonly Queue<(long Ms, long Damage)> Recent = new Queue<(long, long)>();

		public void Reset()
		{
			FirstHitMs = null;
			Damage = 0L;
			EnrageAtMs = null;
			Recent.Clear();
		}
	}

	public const int MaxEntities = 512;

	public const long StaleMs = 600000L;

	private readonly long _windowMs;

	private readonly object _gate = new object();

	private readonly Dictionary<int, Pull> _pulls = new Dictionary<int, Pull>();

	public int TrackedEntityCount
	{
		get
		{
			lock (_gate)
			{
				return _pulls.Count;
			}
		}
	}

	public BossPullClock(long windowMs = 30000L)
	{
		_windowMs = windowMs;
	}

	public void OnDamage(int targetId, int actorId, long damage, bool isHeal, long timestampMs)
	{
		if (((targetId <= 0 || damage <= 0) | isHeal) || actorId == targetId)
		{
			return;
		}
		lock (_gate)
		{
			Pull orAdd = GetOrAdd(targetId, timestampMs);
			orAdd.LastActivityMs = Math.Max(orAdd.LastActivityMs, timestampMs);
			if (timestampMs >= orAdd.AttemptStartMs)
			{
				Pull pull = orAdd;
				long valueOrDefault = pull.FirstHitMs.GetValueOrDefault();
				if (!pull.FirstHitMs.HasValue)
				{
					valueOrDefault = timestampMs;
					pull.FirstHitMs = valueOrDefault;
				}
				orAdd.Damage += damage;
				orAdd.Recent.Enqueue((timestampMs, damage));
				while (orAdd.Recent.Count > 0 && orAdd.Recent.Peek().Ms <= timestampMs - _windowMs)
				{
					orAdd.Recent.Dequeue();
				}
			}
		}
	}

	public void OnBackToFull(int entityId, long timestampMs)
	{
		lock (_gate)
		{
			Pull orAdd = GetOrAdd(entityId, timestampMs);
			orAdd.Reset();
			orAdd.AttemptStartMs = Math.Max(orAdd.AttemptStartMs, timestampMs);
			orAdd.LastActivityMs = Math.Max(orAdd.LastActivityMs, timestampMs);
		}
	}

	public void OnNewLife(int entityId)
	{
		lock (_gate)
		{
			_pulls.Remove(entityId);
		}
	}

	public void OnEnrage(int entityId, long timestampMs)
	{
		if (entityId <= 0)
		{
			return;
		}
		lock (_gate)
		{
			Pull orAdd = GetOrAdd(entityId, timestampMs);
			orAdd.LastActivityMs = Math.Max(orAdd.LastActivityMs, timestampMs);
			if (timestampMs >= orAdd.AttemptStartMs)
			{
				Pull pull = orAdd;
				long valueOrDefault = pull.EnrageAtMs.GetValueOrDefault();
				if (!pull.EnrageAtMs.HasValue)
				{
					valueOrDefault = timestampMs;
					pull.EnrageAtMs = valueOrDefault;
				}
			}
		}
	}

	public BossPullSnapshot Snapshot(int entityId, long nowMs)
	{
		lock (_gate)
		{
			if (!_pulls.TryGetValue(entityId, out Pull value))
			{
				return default;
			}
			long num = 0L;
			foreach (var (num2, num3) in value.Recent)
			{
				if (num2 > nowMs - _windowMs && num2 <= nowMs)
				{
					num += num3;
				}
			}
			return new BossPullSnapshot(value.FirstHitMs, value.Damage, num, value.EnrageAtMs);
		}
	}

	private Pull GetOrAdd(int entityId, long timestampMs)
	{
		if (_pulls.TryGetValue(entityId, out Pull value))
		{
			return value;
		}
		if (_pulls.Count >= 512)
		{
			foreach (int item in (from p in _pulls
				where p.Value.LastActivityMs < timestampMs - 600000
				select p.Key).ToList())
			{
				_pulls.Remove(item);
			}
		}
		value = new Pull
		{
			LastActivityMs = timestampMs
		};
		_pulls[entityId] = value;
		return value;
	}
}
