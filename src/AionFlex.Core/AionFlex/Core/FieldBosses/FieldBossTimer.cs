using AionFlex.Core.Models;

namespace AionFlex.Core.FieldBosses;

public sealed record FieldBossTimer(int ServerId, int RowId, int MapId, bool Alive, long TimeUtcMs, FieldBossPosition? Position, long LastSeenUtcMs)
{
	public bool SameStateAs(FieldBossTimer other)
	{
		if (ServerId == other.ServerId && RowId == other.RowId && MapId == other.MapId && Alive == other.Alive && TimeUtcMs == other.TimeUtcMs)
		{
			FieldBossPosition? position = Position;
			FieldBossPosition? position2 = other.Position;
			if (position.HasValue != position2.HasValue)
			{
				return false;
			}
			if (!position.HasValue)
			{
				return true;
			}
			return position.GetValueOrDefault() == position2.GetValueOrDefault();
		}
		return false;
	}
}
