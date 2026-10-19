using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedStatInfo(long? Might, long? Dexterity, long? Intelligence, long? Constitution, long? Precision, long? Willpower, IReadOnlyList<ResolvedPantheonRow> Pantheon, long? AttackComputed, long? Defense, long? Accuracy, long? Evasion, long? CriticalHit, long? CriticalHitResist, long? HpDisplayed, long? Mp, decimal? CombatSpeedPct, decimal? MoveSpeedPct);
