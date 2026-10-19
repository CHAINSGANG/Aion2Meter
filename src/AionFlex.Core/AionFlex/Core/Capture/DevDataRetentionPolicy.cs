using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Capture;

public static class DevDataRetentionPolicy
{
	public const string ReasonEmpty = "empty";

	public const string ReasonOlderSessionCapture = "older session capture";

	public const string ReasonCapturesOverBudget = "captures over budget";

	public const string ReasonLogsOverBudget = "logs over budget";

	public const string ReasonDebugDumpsOverBudget = "debug dumps over budget";

	private static readonly DevDataCategory[] CaptureCategories = new DevDataCategory[4]
	{
		DevDataCategory.SessionCapture,
		DevDataCategory.RunCapture,
		DevDataCategory.ManualCapture,
		DevDataCategory.OtherCapture
	};

	private static readonly DevDataCategory[] LogCategories = new DevDataCategory[2]
	{
		DevDataCategory.SessionLog,
		DevDataCategory.OtherLog
	};

	private static readonly DevDataCategory[] DebugDumpCategories = new DevDataCategory[1] { DevDataCategory.DebugDump };

	public static IReadOnlyList<DevDataDeletion> Select(IReadOnlyList<DevDataGroup> groups, DevDataBudgets budgets, DateTime nowUtc)
	{
		List<DevDataDeletion> result = new List<DevDataDeletion>();
		HashSet<int> selected = new HashSet<int>();
		int count = Math.Max(0, budgets.KeepPreviousSessions);
		List<int> list = (from i in Enumerable.Range(0, groups.Count)
			orderby groups[i].NewestWriteUtc
			select i).ThenBy((int i) => groups[i].Key, StringComparer.Ordinal).ToList();
		foreach (int item in list)
		{
			if (Deletable(item) && groups[item].SizeBytes == 0L && nowUtc - groups[item].NewestWriteUtc > budgets.EmptyMinAge)
			{
				Choose(item, "empty");
			}
		}
		HashSet<int> hashSet = NewestDeletable(list, DevDataCategory.SessionCapture, count, DeletableSession, groups);
		foreach (int item2 in list)
		{
			if (groups[item2].Category == DevDataCategory.SessionCapture && DeletableSession(item2) && !hashSet.Contains(item2))
			{
				Choose(item2, "older session capture");
			}
		}
		HashSet<int> protectedGroups = new HashSet<int>();
		long total = BudgetedBytes(list, groups, selected, CaptureCategories);
		total = Trim(list, groups, selected, new DevDataCategory[1], total, budgets.CapturesBytes, protectedGroups, Choose, "captures over budget");
		Trim(list, groups, selected, CaptureCategories, total, budgets.CapturesBytes, protectedGroups, Choose, "captures over budget");
		HashSet<int> protectedGroups2 = NewestDeletable(list, DevDataCategory.SessionLog, count, Deletable, groups);
		Trim(list, groups, selected, LogCategories, BudgetedBytes(list, groups, selected, LogCategories), budgets.LogsBytes, protectedGroups2, Choose, "logs over budget");
		Trim(list, groups, selected, DebugDumpCategories, BudgetedBytes(list, groups, selected, DebugDumpCategories), budgets.DebugDumpsBytes, new HashSet<int>(), Choose, "debug dumps over budget");
		return result;
		void Choose(int i, string reason)
		{
			selected.Add(i);
			result.Add(new DevDataDeletion(groups[i], reason));
		}
		bool Deletable(int i)
		{
			if (!groups[i].Pinned && !groups[i].InUse)
			{
				return !selected.Contains(i);
			}
			return false;
		}
		bool DeletableSession(int i)
		{
			if (Deletable(i))
			{
				return groups[i].SizeBytes > 0;
			}
			return false;
		}
	}

	private static HashSet<int> NewestDeletable(List<int> byAge, DevDataCategory category, int count, Func<int, bool> deletable, IReadOnlyList<DevDataGroup> groups)
	{
		HashSet<int> hashSet = new HashSet<int>();
		int num = byAge.Count - 1;
		while (num >= 0 && hashSet.Count < count)
		{
			int num2 = byAge[num];
			if (groups[num2].Category == category && deletable(num2))
			{
				hashSet.Add(num2);
			}
			num--;
		}
		return hashSet;
	}

	private static long BudgetedBytes(List<int> byAge, IReadOnlyList<DevDataGroup> groups, HashSet<int> selected, DevDataCategory[] categories)
	{
		long num = 0L;
		foreach (int item in byAge)
		{
			DevDataGroup devDataGroup = groups[item];
			if (Array.IndexOf(categories, devDataGroup.Category) >= 0 && !devDataGroup.Pinned && !devDataGroup.InUse && !selected.Contains(item))
			{
				num += devDataGroup.SizeBytes;
			}
		}
		return num;
	}

	private static long Trim(List<int> byAge, IReadOnlyList<DevDataGroup> groups, HashSet<int> selected, DevDataCategory[] categories, long total, long budgetBytes, HashSet<int> protectedGroups, Action<int, string> choose, string reason)
	{
		foreach (int item in byAge)
		{
			if (total <= budgetBytes)
			{
				break;
			}
			DevDataGroup devDataGroup = groups[item];
			if (Array.IndexOf(categories, devDataGroup.Category) >= 0 && !devDataGroup.Pinned && !devDataGroup.InUse && !selected.Contains(item) && !protectedGroups.Contains(item) && devDataGroup.SizeBytes > 0)
			{
				choose(item, reason);
				total -= devDataGroup.SizeBytes;
			}
		}
		return total;
	}
}
