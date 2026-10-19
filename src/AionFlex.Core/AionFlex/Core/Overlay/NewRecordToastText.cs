using System;
using System.Globalization;

namespace AionFlex.Core.Overlay;

public sealed record NewRecordToastText(string DpsFigure, string DeltaText, string DeltaShortText, bool HasPrevious)
{
	public const string Title = "NEW PERSONAL BEST";

	public const string FirstRecordText = "First record";

	public const double MultipleFromPercent = 1000.0;

	private const double CutSlack = 1E-09;

	private const string VsPrevious = " vs previous";

	public static NewRecordToastText Build(double recordDps, double? previousDps, CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture, "culture");
		string dpsFigure = ShortDps(recordDps, culture);
		if (previousDps.HasValue)
		{
			double valueOrDefault = previousDps.GetValueOrDefault();
			if (double.IsFinite(valueOrDefault) && !(valueOrDefault <= 0.0))
			{
				return new NewRecordToastText(dpsFigure, Delta(recordDps, valueOrDefault, culture), DeltaShort(recordDps, valueOrDefault, culture), HasPrevious: true);
			}
		}
		return new NewRecordToastText(dpsFigure, "First record", "First record", HasPrevious: false);
	}

	public static string ShortDps(double value, CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture, "culture");
		if (!double.IsFinite(value))
		{
			value = 0.0;
		}
		if (!(value >= 1000000.0))
		{
			if (value >= 1000.0)
			{
				return (value / 1000.0).ToString("F1", culture) + "K";
			}
			return value.ToString("F0", culture);
		}
		return (value / 1000000.0).ToString("F2", culture) + "M";
	}

	public static string Delta(double recordDps, double previousDps, CultureInfo culture)
	{
		string text = DeltaShort(recordDps, previousDps, culture);
		if (text.Length == 0)
		{
			return "";
		}
		if (!text.EndsWith('×'))
		{
			return text + " vs previous";
		}
		return text + " previous";
	}

	public static string DeltaShort(double recordDps, double previousDps, CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture, "culture");
		if (!double.IsFinite(recordDps) || !double.IsFinite(previousDps) || previousDps <= 0.0 || recordDps <= previousDps)
		{
			return "";
		}
		double num = (recordDps - previousDps) / previousDps * 100.0;
		if (num >= 1000.0)
		{
			return (Math.Floor(recordDps / previousDps * 10.0 + 1E-09) / 10.0).ToString("0.#", culture) + "×";
		}
		if (num >= 100.0)
		{
			return "+" + Math.Floor(num + 1E-09).ToString("0", culture) + "%";
		}
		double num2 = Math.Floor(num * 10.0 + 1E-09) / 10.0;
		if (num2 < 0.1)
		{
			return "+<" + 0.1.ToString("0.0", culture) + "%";
		}
		return "+" + num2.ToString("0.0", culture) + "%";
	}
}
