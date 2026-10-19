using System;
using System.Collections.Generic;
using AionFlex.Core.Localization;

namespace AionFlex.Core.Models;

public sealed record ProcBuffSpec
{
    public required int SkillCode { get; init; }
    public required string Name { get; init; }
    public string NameKo { get; init; } = "";
    public string NameZh { get; init; } = "";
    public required IReadOnlyList<string> Triggers { get; init; }
    public required double ChancePct { get; init; }
    public double? ChancePctMax { get; init; }
    public required long IcdMs { get; init; }
    public long? DurationMs { get; init; }
    public long? DurationMsMax { get; init; }
    public IReadOnlyList<int> TriggeredSkills { get; init; } = Array.Empty<int>();
    public bool ReprocWhileActive { get; init; } = true;
    public bool DerivedValidated { get; init; }
    public bool PartyWide { get; init; }
    public IReadOnlyList<int> BuffAbnormals { get; init; } = Array.Empty<int>();
    public bool BuffOnSelf { get; init; } = true;
    public IReadOnlyList<string> HitTypes { get; init; } = Array.Empty<string>();
    public string? Direction { get; init; }
    public bool BossOnly { get; init; }
    public IReadOnlyList<string> CcTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnknownFilters { get; init; } = Array.Empty<string>();

    public string GetName(Language language) => language switch
    {
        Language.Korean when NameKo.Length > 0 => NameKo,
        Language.Chinese when NameZh.Length > 0 => NameZh,
        _ => Name
    };
}
