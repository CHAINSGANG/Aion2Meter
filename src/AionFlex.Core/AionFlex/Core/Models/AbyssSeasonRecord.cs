using System;

namespace AionFlex.Core.Models;

public sealed record AbyssSeasonRecord(string CharacterName, int Grade, long RankingPoints, int Kills, int Deaths, int Assists, int RankDelta, DateTime? BoardSnapshotAt);
