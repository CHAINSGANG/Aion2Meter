using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Overlay;

public static class LocalPlayerRowPolicy
{
	public const int PlaceholderPlayerId = -1;

	public static bool IsLocalPlayer(int playerId, string? nickname, int? localEntityId, string? localName)
	{
		if (localEntityId.HasValue && playerId == localEntityId.Value)
		{
			return true;
		}
		if (!string.IsNullOrWhiteSpace(localName))
		{
			return nickname == localName;
		}
		return false;
	}

	public static bool NeedsPlaceholder(IEnumerable<(int PlayerId, string? Nickname)> rows, int? localEntityId, string? localName)
	{
		// The UI waits for a fresh local-player record before showing any rows.
		// A placeholder only makes sense after the character is positively bound.
		if (!localEntityId.HasValue || string.IsNullOrWhiteSpace(localName))
		{
			return false;
		}
		return !rows.Any(((int PlayerId, string Nickname) r) => IsLocalPlayer(r.PlayerId, r.Nickname, localEntityId, localName));
	}
}
