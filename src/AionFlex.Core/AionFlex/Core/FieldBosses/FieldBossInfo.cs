using AionFlex.Core.Models;

namespace AionFlex.Core.FieldBosses;

public sealed record FieldBossInfo(int RowId, int MapId, int NpcId, int? Level, LocalizedText Name);
