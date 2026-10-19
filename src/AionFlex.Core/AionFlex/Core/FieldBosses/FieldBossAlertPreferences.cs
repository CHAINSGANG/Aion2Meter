using System.Collections.Generic;

namespace AionFlex.Core.FieldBosses;

public sealed record FieldBossAlertPreferences(bool Enabled, int MinutesBefore, bool WhenUp, IReadOnlyCollection<int> BossIds);
