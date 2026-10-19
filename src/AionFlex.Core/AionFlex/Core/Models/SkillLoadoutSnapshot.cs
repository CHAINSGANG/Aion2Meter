using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record SkillLoadoutSnapshot(IReadOnlyList<SkillRosterEntry> Roster, IReadOnlyList<SkillEquippedEntry> Equipped)
{
	public bool IsEmpty
	{
		get
		{
			if (Roster.Count == 0)
			{
				return Equipped.Count == 0;
			}
			return false;
		}
	}

	public static readonly SkillLoadoutSnapshot Empty = new SkillLoadoutSnapshot(Array.Empty<SkillRosterEntry>(), Array.Empty<SkillEquippedEntry>());
}
