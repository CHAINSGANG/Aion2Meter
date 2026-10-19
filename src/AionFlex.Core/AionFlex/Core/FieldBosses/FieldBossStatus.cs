using System;

namespace AionFlex.Core.FieldBosses;

public readonly record struct FieldBossStatus(FieldBossStatusKind Kind, TimeSpan? Remaining, TimeSpan? SinceSeen);
