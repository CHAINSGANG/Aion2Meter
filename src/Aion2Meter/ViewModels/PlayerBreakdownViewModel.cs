using System.Collections.Generic;
using System.Linq;

namespace Aion2Meter.ViewModels;

public sealed class PlayerBreakdownViewModel
{
    public PlayerBreakdownViewModel(PlayerRowViewModel player)
    {
        PlayerId = player.PlayerId;
        Nickname = player.Nickname;
        ClassName = player.ClassName;
        IconPath = player.IconPath;
        DpsText = player.DpsText;
        DamageText = player.DamageText;
        ShareText = player.ShareText;
        HitsText = player.Hits.ToString("N0");
        CritText = player.CritRate.ToString("0.#") + "%";
        RateLabel = player.RateLabel;
        AmountLabel = player.AmountLabel;
        EmptySkillsText = player.EmptySkillsText;
        Skills = player.Skills.Select(skill => new SkillRowViewModel(skill)).ToArray();
    }

    public int PlayerId { get; }
    public string Nickname { get; }
    public string ClassName { get; }
    public string? IconPath { get; }
    public string DpsText { get; }
    public string DamageText { get; }
    public string ShareText { get; }
    public string HitsText { get; }
    public string CritText { get; }
    public string RateLabel { get; }
    public string AmountLabel { get; }
    public string EmptySkillsText { get; }
    public IReadOnlyList<SkillRowViewModel> Skills { get; }
    public bool HasSkills => Skills.Count > 0;
}
