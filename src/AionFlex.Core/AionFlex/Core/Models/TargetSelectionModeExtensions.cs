using System.Collections.Generic;

namespace AionFlex.Core.Models;

public static class TargetSelectionModeExtensions
{
	private static readonly Dictionary<string, TargetSelectionMode> IdMap = new Dictionary<string, TargetSelectionMode>
	{
		{
			"mostDamage",
			TargetSelectionMode.MostDamage
		},
		{
			"mostRecent",
			TargetSelectionMode.MostRecent
		},
		{
			"allTargets",
			TargetSelectionMode.AllTargets
		}
	};

	public static TargetSelectionMode FromId(string? id)
	{
		if (id == null)
		{
			return TargetSelectionMode.MostDamage;
		}
		if (!IdMap.TryGetValue(id, out var value))
		{
			return TargetSelectionMode.MostDamage;
		}
		return value;
	}

	public static string ToId(this TargetSelectionMode mode)
	{
		return mode switch
		{
			TargetSelectionMode.MostDamage => "mostDamage", 
			TargetSelectionMode.MostRecent => "mostRecent", 
			TargetSelectionMode.AllTargets => "allTargets", 
			_ => "mostDamage", 
		};
	}
}
