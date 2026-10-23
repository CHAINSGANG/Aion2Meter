using System;
using System.Globalization;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class SkillRowViewModel
{
    private static CultureInfo UiCulture => CultureInfo.CurrentCulture;

    public SkillRowViewModel(MeterSkillSnapshot skill, string? iconPath)
    {
        Name = skill.Name;
        IconPath = iconPath;
        HitsText = skill.DirectHits.ToString("N0", UiCulture);
        DotHitsText = skill.DotHits > 0 ? skill.DotHits.ToString("N0", UiCulture) : "—";
        CritText = skill.DirectHits > 0 ? FormatPercent(skill.CritRate) : "—";
        BackText = skill.HasHitQualityData ? FormatPercent(skill.BackRate) : "—";
        DoubleText = skill.HasHitQualityData ? FormatPercent(skill.DoubleRate) : "—";
        PerfectText = skill.HasHitQualityData ? FormatPercent(skill.PerfectRate) : "—";
        DpsText = FormatCompact(skill.Dps) + "/" + UiLanguageService.Pick("s", "с");
        AverageText = FormatCompact(skill.AverageDamage);
        MinimumText = skill.Hits > 0 ? FormatCompact(skill.MinimumDamage) : "—";
        MaximumText = FormatCompact(skill.MaximumDamage);
        DamageText = FormatCompact(skill.Damage);
        ShareText = skill.Share.ToString("0.#", UiCulture) + "%";
        BarPercent = Math.Clamp(skill.Share, 0, 100);
        BreakdownTooltip = UiLanguageService.Pick(
            $"Direct: {skill.DirectHits:N0} hits · {FormatCompact(skill.DirectDamage)} damage\nDoT: {skill.DotHits:N0} ticks · {FormatCompact(skill.DotDamage)} damage",
            $"Прямой урон: {skill.DirectHits:N0} уд. · {FormatCompact(skill.DirectDamage)} урона\nПериодический: {skill.DotHits:N0} тиков · {FormatCompact(skill.DotDamage)} урона");
    }

    public string Name { get; }
    public string? IconPath { get; }
    public string HitsText { get; }
    public string DotHitsText { get; }
    public string CritText { get; }
    public string BackText { get; }
    public string DoubleText { get; }
    public string PerfectText { get; }
    public string DpsText { get; }
    public string MinimumText { get; }
    public string AverageText { get; }
    public string MaximumText { get; }
    public string DamageText { get; }
    public string ShareText { get; }
    public double BarPercent { get; }
    public string BreakdownTooltip { get; }

    private static string FormatPercent(double value) => value.ToString("0.#", UiCulture) + "%";

    private static string FormatCompact(double value)
    {
        double absolute = Math.Abs(value);
        if (absolute >= 1_000_000_000)
        {
            return (value / 1_000_000_000d).ToString("0.00", UiCulture) + "b";
        }
        if (absolute >= 1_000_000)
        {
            return (value / 1_000_000d).ToString("0.00", UiCulture) + "m";
        }
        if (absolute >= 1_000)
        {
            return (value / 1_000d).ToString("0.0", UiCulture) + "k";
        }
        return value.ToString("0", UiCulture);
    }
}
