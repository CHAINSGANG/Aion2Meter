using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record CharacterRoster(CharacterRosterKind Kind, IReadOnlyList<CharacterRosterEntry> Entries, int? ServerId)
{
	public bool HasSameContent(CharacterRoster other)
	{
		if ((object)other == null || Kind != other.Kind || ServerId != other.ServerId || Entries.Count != other.Entries.Count)
		{
			return false;
		}
		for (int i = 0; i < Entries.Count; i++)
		{
			if (Entries[i] != other.Entries[i])
			{
				return false;
			}
		}
		return true;
	}
}
