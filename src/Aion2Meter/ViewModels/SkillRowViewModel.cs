using System;
using System.Globalization;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class SkillRowViewModel
{
    private static readonly CultureInfo UiCulture = CultureInfo.GetCultureInfo("ru-RU");

    public SkillRowViewModel(MeterSkillSnapshot skill)
    {
        Name = skill.Name;
        HitsText = skill.Hits.ToString("N0", UiCulture);
        CritText = skill.CritRate.ToString("0.#", UiCulture) + "%";
        BackText = skill.BackRate.ToString("0.#", UiCulture) + "%";
        DoubleText = skill.DoubleRate.ToString("0.#", UiCulture) + "%";
        PerfectText = skill.PerfectRate.ToString("0.#", UiCulture) + "%";
        DpsText = FormatCompact(skill.Dps) + "/с";
        AverageText = FormatCompact(skill.AverageDamage);
        MaximumText = FormatCompact(skill.MaximumDamage);
        DamageText = FormatCompact(skill.Damage);
        ShareText = skill.Share.ToString("0.#", UiCulture) + "%";
        BarPercent = Math.Clamp(skill.Share, 0, 100);
    }

    public string Name { get; }
    public string HitsText { get; }
    public string CritText { get; }
    public string BackText { get; }
    public string DoubleText { get; }
    public string PerfectText { get; }
    public string DpsText { get; }
    public string AverageText { get; }
    public string MaximumText { get; }
    public string DamageText { get; }
    public string ShareText { get; }
    public double BarPercent { get; }

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
