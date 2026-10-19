using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public static class BreakdownRebindPolicy
{
	public const int PlaceholderPlayerId = -1;

	public static bool IsSamePlayer(BreakdownPlayerKey shown, BreakdownPlayerKey candidate)
	{
		string text = Normalize(shown.Nickname);
		string text2 = Normalize(candidate.Nickname);
		if (text != null && text2 != null)
		{
			return string.Equals(text, text2, StringComparison.Ordinal);
		}
		if (shown.PlayerId != -1)
		{
			return shown.PlayerId == candidate.PlayerId;
		}
		return false;
	}

	public static int FindIndex(BreakdownPlayerKey shown, IReadOnlyList<BreakdownPlayerKey> candidates)
	{
		if (candidates == null)
		{
			return -1;
		}
		for (int i = 0; i < candidates.Count; i++)
		{
			if (IsSamePlayer(shown, candidates[i]))
			{
				return i;
			}
		}
		return -1;
	}

	private static string? Normalize(string? name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			return name;
		}
		return null;
	}
}
