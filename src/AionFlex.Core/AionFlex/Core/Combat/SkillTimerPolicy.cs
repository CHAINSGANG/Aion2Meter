using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public sealed class SkillTimerPolicy
{
	private struct Accum
	{
		public long StartMs;

		public long EndMs;

		public long DurationMs;

		public int Stacks;
	}

	public const long MinDurationMs = 3000L;

	private readonly Func<int, int?> _activeSkillOf;

	private readonly Dictionary<int, int?> _cache = new Dictionary<int, int?>();

	private readonly Dictionary<int, Accum> _accum = new Dictionary<int, Accum>();

	private readonly Dictionary<long, int> _slotsPerAbnormal = new Dictionary<long, int>();

	private readonly List<int> _order = new List<int>();

	public SkillTimerPolicy(Func<int, int?> activeSkillOf)
	{
		_activeSkillOf = activeSkillOf ?? throw new ArgumentNullException("activeSkillOf");
	}

	public int? ActiveSkillOf(int sourceSkillId)
	{
		if (sourceSkillId <= 0)
		{
			return null;
		}
		if (!_cache.TryGetValue(sourceSkillId, out var value))
		{
			value = _activeSkillOf(sourceSkillId);
			_cache[sourceSkillId] = value;
		}
		return value;
	}

	public void Evaluate(int localEntityId, IReadOnlyList<OpenBuffWindow> onLocal, int targetId, IReadOnlyList<OpenBuffWindow>? onTarget, long nowMs, bool showSelf, bool showTarget, IReadOnlySet<int> hidden, List<ActiveProcItem> into)
	{
		ArgumentNullException.ThrowIfNull(onLocal, "onLocal");
		ArgumentNullException.ThrowIfNull(hidden, "hidden");
		ArgumentNullException.ThrowIfNull(into, "into");
		into.Clear();
		if (localEntityId > 0)
		{
			if (showSelf)
			{
				CollectAndEmit(onLocal, localEntityId, localEntityId, ProcBarSection.Self, nowMs, hidden, into);
			}
			if (showTarget && onTarget != null && targetId > 0 && targetId != localEntityId)
			{
				CollectAndEmit(onTarget, targetId, localEntityId, ProcBarSection.Target, nowMs, hidden, into);
			}
		}
	}

	public IReadOnlyList<ActiveProcItem> Evaluate(int localEntityId, IReadOnlyList<OpenBuffWindow> onLocal, int targetId, IReadOnlyList<OpenBuffWindow>? onTarget, long nowMs, bool showSelf, bool showTarget, IReadOnlySet<int> hidden)
	{
		List<ActiveProcItem> list = new List<ActiveProcItem>();
		Evaluate(localEntityId, onLocal, targetId, onTarget, nowMs, showSelf, showTarget, hidden, list);
		return list;
	}

	private void CollectAndEmit(IReadOnlyList<OpenBuffWindow> windows, int onEntity, int casterId, ProcBarSection section, long nowMs, IReadOnlySet<int> hidden, List<ActiveProcItem> into)
	{
		_accum.Clear();
		_slotsPerAbnormal.Clear();
		_order.Clear();
		for (int i = 0; i < windows.Count; i++)
		{
			OpenBuffWindow openBuffWindow = windows[i];
			if (openBuffWindow.TargetId != onEntity || openBuffWindow.CasterId != casterId)
			{
				continue;
			}
			long? endMs = openBuffWindow.EndMs;
			if (!endMs.HasValue)
			{
				continue;
			}
			long valueOrDefault = endMs.GetValueOrDefault();
			if (valueOrDefault <= nowMs || (long)openBuffWindow.DurationMs < 3000L)
			{
				continue;
			}
			int? num = ActiveSkillOf(openBuffWindow.SourceSkillId);
			if (!num.HasValue)
			{
				continue;
			}
			int valueOrDefault2 = num.GetValueOrDefault();
			if (!hidden.Contains(valueOrDefault2))
			{
				if (!_accum.TryGetValue(valueOrDefault2, out var value))
				{
					value = new Accum
					{
						StartMs = openBuffWindow.StartMs,
						EndMs = valueOrDefault,
						DurationMs = openBuffWindow.DurationMs
					};
					_order.Add(valueOrDefault2);
				}
				else if (valueOrDefault > value.EndMs)
				{
					value.StartMs = openBuffWindow.StartMs;
					value.EndMs = valueOrDefault;
					value.DurationMs = openBuffWindow.DurationMs;
				}
				long key = ((long)valueOrDefault2 << 32) | (uint)openBuffWindow.AbnormalId;
				int num2 = ((!_slotsPerAbnormal.TryGetValue(key, out var value2)) ? 1 : (value2 + 1));
				_slotsPerAbnormal[key] = num2;
				if (num2 > value.Stacks)
				{
					value.Stacks = num2;
				}
				_accum[valueOrDefault2] = value;
			}
		}
		_order.Sort();
		foreach (int item in _order)
		{
			Accum accum = _accum[item];
			into.Add(new ActiveProcItem(item, section, item, accum.StartMs, accum.EndMs, accum.DurationMs, accum.Stacks));
		}
	}
}
