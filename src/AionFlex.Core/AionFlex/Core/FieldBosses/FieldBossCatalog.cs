using System;
using System.Collections.Generic;

namespace AionFlex.Core.FieldBosses;

public sealed class FieldBossCatalog : IFieldBossCatalog
{
	private readonly Dictionary<int, FieldBossInfo> _byRow = new Dictionary<int, FieldBossInfo>();

	private readonly Dictionary<int, FieldBossMapInfo> _byMap = new Dictionary<int, FieldBossMapInfo>();

	public static FieldBossCatalog Empty { get; } = new FieldBossCatalog(Array.Empty<FieldBossMapInfo>(), Array.Empty<FieldBossInfo>());

	public IReadOnlyList<FieldBossMapInfo> Maps { get; }

	public IReadOnlyList<FieldBossInfo> Bosses { get; }

	public FieldBossCatalog(IEnumerable<FieldBossMapInfo> maps, IEnumerable<FieldBossInfo> bosses)
	{
		List<FieldBossMapInfo> list = new List<FieldBossMapInfo>();
		foreach (FieldBossMapInfo map in maps)
		{
			if (_byMap.TryAdd(map.MapId, map))
			{
				list.Add(map);
			}
		}
		list.Sort((FieldBossMapInfo a, FieldBossMapInfo b) => a.MapId.CompareTo(b.MapId));
		List<FieldBossInfo> list2 = new List<FieldBossInfo>();
		foreach (FieldBossInfo boss in bosses)
		{
			if (_byMap.ContainsKey(boss.MapId) && _byRow.TryAdd(boss.RowId, boss))
			{
				list2.Add(boss);
			}
		}
		list2.Sort((FieldBossInfo a, FieldBossInfo b) => (a.MapId == b.MapId) ? a.RowId.CompareTo(b.RowId) : a.MapId.CompareTo(b.MapId));
		Maps = list;
		Bosses = list2;
	}

	public FieldBossInfo? Find(int rowId)
	{
		if (!_byRow.TryGetValue(rowId, out FieldBossInfo value))
		{
			return null;
		}
		return value;
	}

	public FieldBossMapInfo? FindMap(int mapId)
	{
		if (!_byMap.TryGetValue(mapId, out FieldBossMapInfo value))
		{
			return null;
		}
		return value;
	}
}
