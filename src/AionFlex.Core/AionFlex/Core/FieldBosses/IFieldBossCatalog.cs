using System.Collections.Generic;

namespace AionFlex.Core.FieldBosses;

public interface IFieldBossCatalog
{
	IReadOnlyList<FieldBossMapInfo> Maps { get; }

	IReadOnlyList<FieldBossInfo> Bosses { get; }

	FieldBossInfo? Find(int rowId);

	FieldBossMapInfo? FindMap(int mapId);
}
