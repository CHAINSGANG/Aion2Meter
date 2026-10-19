using System.Collections.Generic;

namespace AionFlex.Core.Combat;

public sealed class BossAttemptCounter
{
	private readonly object _lock = new object();

	private readonly Dictionary<int, int> _wipesByNpc = new Dictionary<int, int>();

	private int _localEntityId;

	public void ObserveLocalEntity(int localEntityId)
	{
		if (localEntityId <= 0)
		{
			return;
		}
		lock (_lock)
		{
			if (_localEntityId != 0 && _localEntityId != localEntityId)
			{
				_wipesByNpc.Clear();
			}
			_localEntityId = localEntityId;
		}
	}

	public void RecordReset(int npcId)
	{
		if (npcId <= 0)
		{
			return;
		}
		lock (_lock)
		{
			_wipesByNpc[npcId] = ((!_wipesByNpc.TryGetValue(npcId, out var value)) ? 1 : (value + 1));
		}
	}

	public int? WipesBefore(int npcId)
	{
		if (npcId <= 0)
		{
			return null;
		}
		lock (_lock)
		{
			int value;
			return _wipesByNpc.TryGetValue(npcId, out value) ? value : 0;
		}
	}

	public void Complete(int npcId)
	{
		if (npcId <= 0)
		{
			return;
		}
		lock (_lock)
		{
			_wipesByNpc.Remove(npcId);
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_wipesByNpc.Clear();
		}
	}
}
