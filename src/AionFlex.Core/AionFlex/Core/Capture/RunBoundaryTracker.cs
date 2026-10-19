using System;
using System.Collections.Generic;

namespace AionFlex.Core.Capture;

public sealed class RunBoundaryTracker
{
	private sealed class MutableRun
	{
		public int Sequence;

		public long AnchorMs;

		public RunAnchorKind AnchorKind;

		public long LastActivityMs;

		public int? ServerId;

		public int? DungeonId;

		public long? LiveStartMs;

		public long? EndedMs;

		public long? ResultMs;

		public bool? Cleared;
	}

	public const long TransferDedupWindowMs = 5000L;

	public const long MissedRestartLeadMs = 35000L;

	public const long IdleCloseMs = 600000L;

	public const long PreRollSignalMs = 30000L;

	public const long PreRollNpcMs = 120000L;

	public const long PostRollMs = 30000L;

	private const int StateRunLive = 2;

	private const int StateEndCountdown = 3;

	private const int StateRestartCountdown = 4;

	private static readonly IReadOnlyList<ClosedRun> None = Array.Empty<ClosedRun>();

	private readonly HashSet<(int State, long StartMs)> _seenStates = new HashSet<(int, long)>();

	private MutableRun? _open;

	private int _nextSequence = 1;

	private int? _currentServerId;

	private int? _lastTransferServerId;

	private long _lastTransferMs;

	private long _maxLiveStartMs = long.MinValue;

	public int? HomeServerId { get; private set; }

	public OpenRun? Current
	{
		get
		{
			if (_open != null)
			{
				return new OpenRun(_open.Sequence, _open.AnchorMs, _open.AnchorKind, _open.ServerId, _open.DungeonId, _open.LiveStartMs);
			}
			return null;
		}
	}

	public void SetHomeServerId(int serverId)
	{
		HomeServerId = serverId;
		MutableRun open = _open;
		if (open == null || open.AnchorKind != RunAnchorKind.ServerTransfer || open.ServerId != serverId)
		{
			return;
		}
		int? dungeonId = open.DungeonId;
		if (dungeonId.HasValue)
		{
			return;
		}
		long? liveStartMs = open.LiveStartMs;
		if (liveStartMs.HasValue)
		{
			return;
		}
		liveStartMs = open.EndedMs;
		if (!liveStartMs.HasValue)
		{
			liveStartMs = open.ResultMs;
			if (!liveStartMs.HasValue)
			{
				_open = null;
				_nextSequence = open.Sequence;
			}
		}
	}

	public IReadOnlyList<ClosedRun> OnServerTransfer(int serverId, long tMs)
	{
		if (_lastTransferServerId == serverId && tMs - _lastTransferMs < 5000)
		{
			return None;
		}
		List<ClosedRun> list = CloseIfIdle(tMs);
		_lastTransferServerId = serverId;
		_lastTransferMs = tMs;
		_currentServerId = serverId;
		if (_open != null)
		{
			list = Add(list, Close(tMs, RunCloseKind.ServerTransfer));
		}
		if (serverId != HomeServerId)
		{
			Open(tMs, RunAnchorKind.ServerTransfer, serverId);
		}
		IReadOnlyList<ClosedRun> readOnlyList = list;
		return readOnlyList ?? None;
	}

	public IReadOnlyList<ClosedRun> OnRunState(int dungeonId, int state, long startMs, long endMs, long tMs)
	{
		if (!_seenStates.Add((state, startMs)))
		{
			return None;
		}
		List<ClosedRun> list = CloseIfIdle(tMs);
		if (state == 4)
		{
			if (_open != null)
			{
				list = Add(list, Close(tMs, RunCloseKind.RestartCountdown));
			}
			Open(tMs, RunAnchorKind.RestartCountdown, _currentServerId);
		}
		else if (state == 2 && startMs > _maxLiveStartMs)
		{
			_maxLiveStartMs = startMs;
			if (_open == null)
			{
				Open(tMs - 35000, RunAnchorKind.RunLiveStart, _currentServerId);
			}
			else
			{
				long? liveStartMs = _open.LiveStartMs;
				if (liveStartMs.HasValue)
				{
					long num = Math.Max(tMs - 35000, _open.AnchorMs);
					long lastActivityMs = _open.LastActivityMs;
					list = Add(list, Close(num, RunCloseKind.RunLiveStart));
					Open(num, RunAnchorKind.RunLiveStart, _currentServerId);
					_open.LastActivityMs = Math.Max(num, lastActivityMs);
				}
			}
			_open.LiveStartMs = startMs;
		}
		else if (state == 3 && _open != null)
		{
			_open.EndedMs = tMs;
		}
		if (_open != null)
		{
			_open.DungeonId = dungeonId;
			Touch(tMs);
		}
		IReadOnlyList<ClosedRun> readOnlyList = list;
		return readOnlyList ?? None;
	}

	public void OnRunResult(int dungeonId, bool cleared, long tMs)
	{
		if (_open == null)
		{
			return;
		}
		long? resultMs = _open.ResultMs;
		if (!resultMs.HasValue)
		{
			_open.Cleared = cleared;
			_open.ResultMs = tMs;
			MutableRun open = _open;
			int valueOrDefault = open.DungeonId.GetValueOrDefault();
			if (!open.DungeonId.HasValue)
			{
				valueOrDefault = dungeonId;
				open.DungeonId = valueOrDefault;
			}
			Touch(tMs);
		}
	}

	public IReadOnlyList<ClosedRun> OnInstanceNpc(long tMs)
	{
		List<ClosedRun>? list = CloseIfIdle(tMs);
		if (_open == null)
		{
			Open(tMs, RunAnchorKind.InstanceNpc, _currentServerId);
		}
		IReadOnlyList<ClosedRun> readOnlyList = list;
		return readOnlyList ?? None;
	}

	public void OnActivity(long tMs)
	{
		Touch(tMs);
	}

	public IReadOnlyList<ClosedRun> Tick(long nowMs)
	{
		IReadOnlyList<ClosedRun> readOnlyList = CloseIfIdle(nowMs);
		return readOnlyList ?? None;
	}

	public IReadOnlyList<ClosedRun> Finish(long nowMs)
	{
		List<ClosedRun> list = CloseIfIdle(nowMs);
		if (_open != null)
		{
			list = Add(list, Close(nowMs, RunCloseKind.SessionEnd));
		}
		IReadOnlyList<ClosedRun> readOnlyList = list;
		return readOnlyList ?? None;
	}

	public static (long FromMs, long ToMs) CaptureWindow(ClosedRun run, long sessionStartMs, long sessionEndMs)
	{
		long num = ((run.AnchorKind == RunAnchorKind.InstanceNpc) ? 120000 : 30000);
		long num2 = Math.Max(run.AnchorMs - num, sessionStartMs);
		long item = Math.Max(Math.Min(((run.CloseKind == RunCloseKind.Idle) ? run.LastActivityMs : run.CloseMs) + 30000, sessionEndMs), num2);
		return (FromMs: num2, ToMs: item);
	}

	private void Open(long anchorMs, RunAnchorKind kind, int? serverId)
	{
		_open = new MutableRun
		{
			Sequence = _nextSequence++,
			AnchorMs = anchorMs,
			AnchorKind = kind,
			LastActivityMs = anchorMs,
			ServerId = serverId
		};
	}

	private ClosedRun Close(long closeMs, RunCloseKind kind)
	{
		MutableRun open = _open;
		_open = null;
		closeMs = Math.Max(closeMs, open.AnchorMs);
		return new ClosedRun(open.Sequence, open.AnchorMs, open.AnchorKind, closeMs, kind, Math.Min(open.LastActivityMs, closeMs), open.ServerId, open.DungeonId, open.LiveStartMs, open.EndedMs, open.ResultMs, open.Cleared);
	}

	private List<ClosedRun>? CloseIfIdle(long nowMs)
	{
		if (_open == null)
		{
			return null;
		}
		long num = Math.Max(_open.LastActivityMs, _open.AnchorMs);
		if (nowMs - num < 600000)
		{
			return null;
		}
		return Add(null, Close(num, RunCloseKind.Idle));
	}

	private void Touch(long tMs)
	{
		if (_open != null && tMs > _open.LastActivityMs)
		{
			_open.LastActivityMs = tMs;
		}
	}

	private static List<ClosedRun> Add(List<ClosedRun>? list, ClosedRun run)
	{
		if (list == null)
		{
			list = new List<ClosedRun>(2);
		}
		list.Add(run);
		return list;
	}
}
