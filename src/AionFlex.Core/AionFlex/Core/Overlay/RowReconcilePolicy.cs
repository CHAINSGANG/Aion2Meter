using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AionFlex.Core.Overlay;

public static class RowReconcilePolicy
{
	public static IReadOnlyList<RowEdit> Plan<T>(IReadOnlyList<T> current, IReadOnlyList<T> desired, IEqualityComparer<T>? comparer = null)
	{
		if (current == null)
		{
			throw new ArgumentNullException("current");
		}
		if (desired == null)
		{
			throw new ArgumentNullException("desired");
		}
		if (comparer == null)
		{
			comparer = EqualityComparer<T>.Default;
		}
		if (current.Count == desired.Count)
		{
			bool flag = true;
			for (int i = 0; i < current.Count; i++)
			{
				if (!comparer.Equals(current[i], desired[i]))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return Array.Empty<RowEdit>();
			}
		}
		List<RowEdit> list = new List<RowEdit>();
		List<T> list2 = new List<T>(current);
		HashSet<T> hashSet = new HashSet<T>(desired, comparer);
		for (int num = list2.Count - 1; num >= 0; num--)
		{
			if (!hashSet.Contains(list2[num]))
			{
				list.Add(RowEdit.RemoveAt(num));
				list2.RemoveAt(num);
			}
		}
		for (int j = 0; j < desired.Count; j++)
		{
			T val = desired[j];
			int num2 = -1;
			for (int k = j; k < list2.Count; k++)
			{
				if (comparer.Equals(list2[k], val))
				{
					num2 = k;
					break;
				}
			}
			if (num2 < 0)
			{
				list.Add(RowEdit.Insert(j));
				list2.Insert(j, val);
			}
			else if (num2 != j)
			{
				list.Add(RowEdit.Move(num2, j));
				list2.RemoveAt(num2);
				list2.Insert(j, val);
			}
		}
		for (int num3 = list2.Count - 1; num3 >= desired.Count; num3--)
		{
			list.Add(RowEdit.RemoveAt(num3));
			list2.RemoveAt(num3);
		}
		return list;
	}

	public static void Apply<T>(ObservableCollection<T> target, IReadOnlyList<T> desired, IReadOnlyList<RowEdit> plan)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		if (desired == null)
		{
			throw new ArgumentNullException("desired");
		}
		if (plan == null)
		{
			throw new ArgumentNullException("plan");
		}
		for (int i = 0; i < plan.Count; i++)
		{
			RowEdit rowEdit = plan[i];
			switch (rowEdit.Kind)
			{
			case RowEditKind.RemoveAt:
				target.RemoveAt(rowEdit.From);
				break;
			case RowEditKind.Insert:
				target.Insert(rowEdit.To, desired[rowEdit.To]);
				break;
			case RowEditKind.Move:
				target.Move(rowEdit.From, rowEdit.To);
				break;
			}
		}
	}

	public static IReadOnlyList<RowEdit> Reconcile<T>(ObservableCollection<T> target, IReadOnlyList<T> desired, IEqualityComparer<T>? comparer = null)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		if (desired == null)
		{
			throw new ArgumentNullException("desired");
		}
		IReadOnlyList<RowEdit> readOnlyList = Plan(target, desired, comparer);
		if (readOnlyList.Count > 0)
		{
			Apply(target, desired, readOnlyList);
		}
		return readOnlyList;
	}
}
