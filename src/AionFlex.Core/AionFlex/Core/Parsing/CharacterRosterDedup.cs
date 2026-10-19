using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public sealed class CharacterRosterDedup
{
	public const long WindowMs = 2000L;

	private CharacterRoster? _last;

	private long _lastMs;

	public bool ShouldEmit(CharacterRoster roster, long nowMs)
	{
		if ((object)_last != null && _last.HasSameContent(roster) && nowMs - _lastMs < 2000)
		{
			return false;
		}
		_last = roster;
		_lastMs = nowMs;
		return true;
	}
}
