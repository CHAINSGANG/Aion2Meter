using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public sealed class PartyRoomRosterDedup
{
	public const long WindowMs = 2000L;

	private PartyRoomRoster? _last;

	private long _lastMs;

	public bool ShouldEmit(PartyRoomRoster roster, long nowMs)
	{
		if ((object)_last != null && _last.HasSameContent(roster) && nowMs - _lastMs < 2000)
		{
			return false;
		}
		_last = roster;
		_lastMs = nowMs;
		return true;
	}

	public void Reset()
	{
		_last = null;
		_lastMs = 0L;
	}
}
