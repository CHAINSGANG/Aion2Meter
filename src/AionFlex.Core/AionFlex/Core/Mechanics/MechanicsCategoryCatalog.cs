using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Mechanics;

public static class MechanicsCategoryCatalog
{
	private static readonly MechanicCategory[] Order = new MechanicCategory[3]
	{
		MechanicCategory.Expedition,
		MechanicCategory.Transcendence,
		MechanicCategory.Sanctuary
	};

	public static IReadOnlyList<MechanicCategory> DisplayOrder => Order;

	public static int Rank(MechanicCategory category)
	{
		for (int i = 0; i < Order.Length; i++)
		{
			if (Order[i] == category)
			{
				return i;
			}
		}
		return int.MaxValue;
	}

	public static IReadOnlyList<MechanicCategory> PresentIn(IEnumerable<BossMechanics> bosses)
	{
		HashSet<MechanicCategory> hashSet = new HashSet<MechanicCategory>();
		foreach (BossMechanics boss in bosses)
		{
			hashSet.Add(boss.Category);
		}
		List<MechanicCategory> list = new List<MechanicCategory>(Order.Length);
		MechanicCategory[] order = Order;
		foreach (MechanicCategory item in order)
		{
			if (hashSet.Contains(item))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public static LocalizedText Label(MechanicCategory category)
	{
		return category switch
		{
			MechanicCategory.Expedition => new LocalizedText
			{
				En = "Expedition",
				Ko = "원정",
				Zh = "遠征"
			}, 
			MechanicCategory.Transcendence => new LocalizedText
			{
				En = "Transcendence",
				Ko = "초월",
				Zh = "超越"
			}, 
			MechanicCategory.Sanctuary => new LocalizedText
			{
				En = "Sanctuary",
				Ko = "성역",
				Zh = "聖域"
			}, 
			_ => new LocalizedText
			{
				En = category.ToString()
			}, 
		};
	}
}
