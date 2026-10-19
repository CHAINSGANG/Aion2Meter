using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public sealed class FightMarkerRecorder
{
	private enum GaugePhase
	{
		Idle,
		Draining,
		Broken
	}

	private sealed class GaugeState
	{
		public GaugePhase Phase;

		public long? RefilledAt;
	}

	public const int MaxRecords = 4096;

	public const long CastCopyWindowMs = 500L;

	public const long StaggerRearmMs = 1000L;

	public const long DeathCopyWindowMs = 3000L;

	public const double BrokenFraction = 0.05;

	private readonly object _lock = new object();

	private readonly Queue<(long Ts, int Caster, int Skill)> _casts = new Queue<(long, int, int)>();

	private readonly Queue<(long Ts, int Entity, FightMarkerKind Kind)> _gauge = new Queue<(long, int, FightMarkerKind)>();

	private readonly Queue<(long Ts, int Entity)> _deaths = new Queue<(long, int)>();

	private readonly Dictionary<int, GaugeState> _gaugeStates = new Dictionary<int, GaugeState>();

	private readonly Dictionary<int, (uint Hp, long? LastDeathTs)> _hp = new Dictionary<int, (uint, long?)>();

	public void RecordCast(int casterId, int skillCode, long timestampMs)
	{
		if (casterId <= 0 || skillCode <= 0)
		{
			return;
		}
		lock (_lock)
		{
			Add(_casts, (timestampMs, casterId, skillCode));
		}
	}

	public void RecordGauge(int entityId, uint current, uint max, long timestampMs)
	{
		if (entityId <= 0 || max == 0)
		{
			return;
		}
		lock (_lock)
		{
			if (!_gaugeStates.TryGetValue(entityId, out GaugeState value))
			{
				value = (_gaugeStates[entityId] = new GaugeState());
			}
			if (current >= max)
			{
				if (value.Phase != GaugePhase.Idle)
				{
					value.RefilledAt = timestampMs;
				}
				value.Phase = GaugePhase.Idle;
				return;
			}
			if (value.Phase == GaugePhase.Idle)
			{
				long? refilledAt = value.RefilledAt;
				if (refilledAt.HasValue)
				{
					long valueOrDefault = refilledAt.GetValueOrDefault();
					if (timestampMs - valueOrDefault <= 1000)
					{
						return;
					}
				}
				value.Phase = GaugePhase.Draining;
				Add(_gauge, (timestampMs, entityId, FightMarkerKind.Stagger));
			}
			if (value.Phase == GaugePhase.Draining && (double)current <= (double)max * 0.05)
			{
				value.Phase = GaugePhase.Broken;
				Add(_gauge, (timestampMs, entityId, FightMarkerKind.StaggerBroken));
			}
		}
	}

	public void RecordHp(int entityId, uint hp, long timestampMs)
	{
		if (entityId <= 0)
		{
			return;
		}
		lock (_lock)
		{
			bool flag = _hp.TryGetValue(entityId, out (uint, long?) value);
			long? item = (flag ? value.Item2 : ((long?)null));
			if (((hp == 0) & flag) && value.Item1 != 0)
			{
				if (item.HasValue)
				{
					long valueOrDefault = item.GetValueOrDefault();
					if (timestampMs - valueOrDefault <= 3000)
					{
						goto IL_0085;
					}
				}
				Add(_deaths, (timestampMs, entityId));
				item = timestampMs;
			}
			goto IL_0085;
			IL_0085:
			_hp[entityId] = (hp, item);
			if (_hp.Count > 4096)
			{
				_hp.Clear();
			}
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_casts.Clear();
			_gauge.Clear();
			_deaths.Clear();
			_gaugeStates.Clear();
			_hp.Clear();
		}
	}

	public List<FightMarker> Build(int bossEntityId, IReadOnlyDictionary<int, string> players, long startMs, long endMs, Func<int, string?> skillName, long tailMs = 2000L)
	{
		List<FightMarker> list = new List<FightMarker>();
		if (endMs < startMs)
		{
			return list;
		}
		long length = endMs - startMs;
		lock (_lock)
		{
			Dictionary<int, long> dictionary = new Dictionary<int, long>();
			foreach (var (num, num2, num3) in _casts.OrderBy(((long Ts, int Caster, int Skill) c) => c.Ts))
			{
				if (num2 == bossEntityId && Inside(num) && (!dictionary.TryGetValue(num3, out var value) || num - value > 500))
				{
					dictionary[num3] = num;
					string text = skillName(num3);
					list.Add(new FightMarker
					{
						OffsetMs = Offset(num),
						Kind = FightMarkerKind.Cast,
						Id = num3,
						Text = (string.IsNullOrWhiteSpace(text) ? "Unknown skill" : text)
					});
				}
			}
			foreach (var (ts, num4, fightMarkerKind) in _gauge)
			{
				if (num4 == bossEntityId && Inside(ts))
				{
					list.Add(new FightMarker
					{
						OffsetMs = Offset(ts),
						Kind = fightMarkerKind,
						Text = ((fightMarkerKind == FightMarkerKind.Stagger) ? "Stagger" : "Stagger broken")
					});
				}
			}
			foreach (var (ts2, num5) in _deaths)
			{
				if (players.TryGetValue(num5, out string value2) && Inside(ts2))
				{
					list.Add(new FightMarker
					{
						OffsetMs = Offset(ts2),
						Kind = FightMarkerKind.Death,
						Id = num5,
						Text = value2
					});
				}
			}
		}
		return (from m in list
			orderby m.OffsetMs, (int)m.Kind
			select m).ToList();
		bool Inside(long num6)
		{
			if (num6 >= startMs)
			{
				return num6 <= endMs + tailMs;
			}
			return false;
		}
		int Offset(long num6)
		{
			return (int)Math.Min(length, num6 - startMs);
		}
	}

	private static void Add<T>(Queue<T> queue, T item)
	{
		queue.Enqueue(item);
		while (queue.Count > 4096)
		{
			queue.Dequeue();
		}
	}
}
