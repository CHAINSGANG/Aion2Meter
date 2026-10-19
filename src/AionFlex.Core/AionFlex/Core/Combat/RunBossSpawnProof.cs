using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Combat;

public sealed class RunBossSpawnProof
{
	public const int RunEndedState = 3;

	public const int RestartCountdownState = 4;

	private readonly object _lock = new object();

	private readonly HashSet<int> _npcIds = new HashSet<int>();

	private int _localEntityId;

	private long _newestRunEndedStartMs = long.MinValue;

	private long _newestRestartStartMs = long.MinValue;

	public void OnBossSpawn(int npcId)
	{
		if (npcId <= 0)
		{
			return;
		}
		lock (_lock)
		{
			_npcIds.Add(npcId);
		}
	}

	public bool OnLocalEntity(int localEntityId)
	{
		if (localEntityId <= 0)
		{
			return false;
		}
		lock (_lock)
		{
			bool num = _localEntityId != 0 && _localEntityId != localEntityId;
			_localEntityId = localEntityId;
			if (num)
			{
				_npcIds.Clear();
			}
			return num;
		}
	}

	public bool OnRunState(int state, long startMs)
	{
		lock (_lock)
		{
			if (state != 3)
			{
				if (state != 4 || startMs <= _newestRestartStartMs)
				{
					goto IL_0041;
				}
				_newestRestartStartMs = startMs;
			}
			else
			{
				if (startMs <= _newestRunEndedStartMs)
				{
					goto IL_0041;
				}
				_newestRunEndedStartMs = startMs;
			}
			_npcIds.Clear();
			return true;
			IL_0041:
			return false;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_npcIds.Clear();
		}
	}

	public IReadOnlyCollection<int> Snapshot()
	{
		lock (_lock)
		{
			return _npcIds.ToArray();
		}
	}
}
