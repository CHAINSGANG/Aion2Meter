using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public sealed class ActiveProcPolicy
{
	private sealed record Group(int SkillCode, ProcBarSection Section, int Order, ProcBuffSpec Spec);

	private struct Accum
	{
		public bool Seen;

		public long StartMs;

		public long EndMs;

		public long DurationMs;

		public int Stacks;
	}

	private readonly Dictionary<int, int> _groupByAbnormal = new Dictionary<int, int>();

	private readonly List<Group> _groups = new List<Group>();

	private readonly Accum[] _accum;

	private readonly List<int> _touched = new List<int>();

	private readonly Dictionary<long, int> _slotsPerAbnormal = new Dictionary<long, int>();

	public int GroupCount => _groups.Count;

	public ActiveProcPolicy(IReadOnlyList<ProcBuffSpec> specs)
	{
		ArgumentNullException.ThrowIfNull(specs, "specs");
		Dictionary<(int, ProcBarSection), int> dictionary = new Dictionary<(int, ProcBarSection), int>();
		foreach (ProcBuffSpec spec in specs)
		{
			if (spec.BuffAbnormals.Count == 0)
			{
				continue;
			}
			ProcBarSection procBarSection = ((!spec.BuffOnSelf) ? ProcBarSection.Target : ProcBarSection.Self);
			(int, ProcBarSection) key = (spec.SkillCode, procBarSection);
			if (!dictionary.TryGetValue(key, out var value))
			{
				value = (dictionary[key] = _groups.Count);
				_groups.Add(new Group(spec.SkillCode, procBarSection, value, spec));
			}
			foreach (int buffAbnormal in spec.BuffAbnormals)
			{
				_groupByAbnormal.TryAdd(buffAbnormal, value);
			}
		}
		_accum = new Accum[_groups.Count];
	}

	public ProcBuffSpec SpecOf(ActiveProcItem item)
	{
		return _groups[item.Order].Spec;
	}

	public void Evaluate(int localEntityId, IReadOnlyList<OpenBuffWindow> onLocal, int targetId, IReadOnlyList<OpenBuffWindow>? onTarget, long nowMs, bool showTarget, List<ActiveProcItem> into)
	{
		ArgumentNullException.ThrowIfNull(onLocal, "onLocal");
		ArgumentNullException.ThrowIfNull(into, "into");
		into.Clear();
		if (localEntityId > 0)
		{
			Collect(onLocal, localEntityId, localEntityId, ProcBarSection.Self, nowMs);
			Emit(ProcBarSection.Self, into);
			if (showTarget && onTarget != null && targetId > 0 && targetId != localEntityId)
			{
				Collect(onTarget, targetId, localEntityId, ProcBarSection.Target, nowMs);
				Emit(ProcBarSection.Target, into);
			}
		}
	}

	public IReadOnlyList<ActiveProcItem> Evaluate(int localEntityId, IReadOnlyList<OpenBuffWindow> onLocal, int targetId, IReadOnlyList<OpenBuffWindow>? onTarget, long nowMs, bool showTarget)
	{
		List<ActiveProcItem> list = new List<ActiveProcItem>();
		Evaluate(localEntityId, onLocal, targetId, onTarget, nowMs, showTarget, list);
		return list;
	}

	private void Collect(IReadOnlyList<OpenBuffWindow> windows, int onEntity, int casterId, ProcBarSection section, long nowMs)
	{
		_slotsPerAbnormal.Clear();
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
			if (valueOrDefault > nowMs && _groupByAbnormal.TryGetValue(openBuffWindow.AbnormalId, out var value) && _groups[value].Section == section)
			{
				ref Accum reference = ref _accum[value];
				if (!reference.Seen)
				{
					reference = new Accum
					{
						Seen = true,
						StartMs = openBuffWindow.StartMs,
						EndMs = valueOrDefault,
						DurationMs = openBuffWindow.DurationMs,
						Stacks = 0
					};
					_touched.Add(value);
				}
				else if (valueOrDefault > reference.EndMs)
				{
					reference.StartMs = openBuffWindow.StartMs;
					reference.EndMs = valueOrDefault;
					reference.DurationMs = openBuffWindow.DurationMs;
				}
				long key = ((long)value << 32) | (uint)openBuffWindow.AbnormalId;
				int num = ((!_slotsPerAbnormal.TryGetValue(key, out var value2)) ? 1 : (value2 + 1));
				_slotsPerAbnormal[key] = num;
				if (num > reference.Stacks)
				{
					reference.Stacks = num;
				}
			}
		}
	}

	private void Emit(ProcBarSection section, List<ActiveProcItem> into)
	{
		if (_touched.Count == 0)
		{
			return;
		}
		_touched.Sort();
		foreach (int item in _touched)
		{
			ref Accum reference = ref _accum[item];
			Group obj = _groups[item];
			if (obj.Section == section)
			{
				into.Add(new ActiveProcItem(obj.SkillCode, section, obj.Order, reference.StartMs, reference.EndMs, reference.DurationMs, reference.Stacks));
			}
			reference = default;
		}
		_touched.Clear();
	}
}
