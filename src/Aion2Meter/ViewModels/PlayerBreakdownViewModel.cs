using System.Collections.Generic;
using System.Linq;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class PlayerBreakdownViewModel : BindableBase
{
    private static readonly SkillIconService SkillIcons = new();
    private static readonly SkillSpecialtyCatalog SpecialtyCatalog = new();
    private IReadOnlyList<PassiveSkillRowViewModel> _passives = [];
    private IReadOnlyList<SkillSpecialtyRowViewModel> _specialties = [];

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
        Skills = player.Skills.Select(skill => new SkillRowViewModel(skill, SkillIcons.GetIconPath(skill.SkillCode))).ToArray();
        UpdatePassives(player.Passives);
        UpdateSpecialties(player.Specialties);
        PassivesEmptyText = player.IsLocalPlayer
            ? UiLanguageService.Pick(
                "Passive loadout has not been captured yet.",
                "Данные о пассивках ещё не получены.")
            : UiLanguageService.Pick(
                "The game only exposes the local character's selected passives to this meter.",
                "Игра передаёт метру выбранные пассивки только вашего персонажа.");
        SpecialtiesEmptyText = player.IsLocalPlayer
            ? UiLanguageService.Pick(
                "Selected specialties have not been captured yet.",
                "Выбранные особенности ещё не получены.")
            : UiLanguageService.Pick(
                "The game only exposes the local character's selected specialties to this meter.",
                "Игра передаёт метру выбранные особенности только вашего персонажа.");
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
    public IReadOnlyList<PassiveSkillRowViewModel> Passives
    {
        get => _passives;
        private set
        {
            if (SetProperty(ref _passives, value))
                OnPropertyChanged(nameof(HasPassives));
        }
    }
    public bool HasPassives => Passives.Count > 0;
    public string PassivesEmptyText { get; }
    public IReadOnlyList<SkillSpecialtyRowViewModel> Specialties
    {
        get => _specialties;
        private set
        {
            if (SetProperty(ref _specialties, value))
                OnPropertyChanged(nameof(HasSpecialties));
        }
    }
    public bool HasSpecialties => Specialties.Count > 0;
    public string SpecialtiesEmptyText { get; }

    public void UpdatePassives(IReadOnlyList<MeterPassiveSkillSnapshot> passives) =>
        Passives = passives.Select(passive => new PassiveSkillRowViewModel(
            passive.Name,
            SkillIcons.GetIconPath(passive.SkillCode),
            passive.IsEquipped
                ? UiLanguageService.Pick($"Selected · Lv. {passive.Level}", $"Выбрано · ур. {passive.Level}")
                : UiLanguageService.Pick($"Learned · Lv. {passive.Level}", $"Изучено · ур. {passive.Level}"))).ToArray();

    public void UpdateSpecialties(IReadOnlyList<MeterSkillSpecialtySnapshot> specialties) =>
        Specialties = specialties.Select(skill => new SkillSpecialtyRowViewModel(
            skill.SkillName,
            SkillIcons.GetIconPath(skill.SkillCode),
            skill.Level > 0
                ? UiLanguageService.Pick($"Lv. {skill.Level}", $"Ур. {skill.Level}")
                : string.Empty,
            Enumerable.Range(0, 3)
                .Select(index => new SpecialtySlotViewModel(
                    index < skill.SelectedOptions.Count ? skill.SelectedOptions[index].ToString() : "·",
                    index < skill.SelectedOptions.Count,
                    index < skill.SelectedOptions.Count
                        ? SpecialtyCatalog.GetOptionIconPath(skill.SkillCode, skill.SelectedOptions[index])
                        : null))
                .ToArray(),
            skill.SelectedOptions
                .Select(option => new SpecialtyOptionRowViewModel(
                    option.ToString(),
                    SpecialtyCatalog.GetDescription(skill.SkillCode, option),
                    SpecialtyCatalog.GetOptionIconPath(skill.SkillCode, option)))
                .ToArray())).ToArray();
}

public sealed record PassiveSkillRowViewModel(string Name, string? IconPath, string StatusText);
public sealed record SkillSpecialtyRowViewModel(
    string Name,
    string? IconPath,
    string LevelText,
    IReadOnlyList<SpecialtySlotViewModel> Slots,
    IReadOnlyList<SpecialtyOptionRowViewModel> SelectedOptions);
public sealed record SpecialtySlotViewModel(string Number, bool IsSelected, string? IconPath);
public sealed record SpecialtyOptionRowViewModel(string Number, string Description, string? IconPath);
