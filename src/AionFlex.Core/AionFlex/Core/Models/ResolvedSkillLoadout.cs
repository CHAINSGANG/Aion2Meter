using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record ResolvedSkillLoadout(IReadOnlyList<ResolvedSkillEntry> Actives, IReadOnlyList<ResolvedSkillEntry> Passives, IReadOnlyList<ResolvedSkillEntry> Stigmas);
