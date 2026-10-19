using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AionFlex.Core.Combat;

namespace AionFlex.Core.Overlay.Themes;

public sealed class LegionThemeSpec : OverlayThemeSpec
{
	public readonly record struct NameLineExtras(bool CombatPower, bool Mitigation, bool Globe);

	public readonly record struct NameLineRow(double NameWidth, double CpWidth);

	public readonly record struct LegionRowColors(uint FillTop, uint FillBottom, uint Rim);

	public const uint Ink = uint.MaxValue;

	public const uint InkMuted = 3355443199u;

	public const uint TextOutline = 3640655872u;

	public const uint CombatPowerInk = 4286571504u;

	public const uint ShareInk = 4294106218u;

	public const double TextOutlineThickness = 2.0;

	public const double SmallTextOutlineThickness = 1.5;

	public const double FigureOutlineThickness = 0.0;

	public const uint MitInk = 3774873599u;

	public const uint UnitInk = 3774873599u;

	public const double DpsUnitSpacing = 0.5;

	public const uint Track = 1073741824u;

	public const uint YouRim = 3439329279u;

	public const byte FillAlpha = 235;

	public const double FillTopWeight = 0.56;

	public const double FillBottomWeight = 0.44;

	public const double RankOneLift = 0.06;

	public const double ContentPadLeft = 4.0;

	public const double NameMarginLeft = 2.0;

	public const double IconNameGap = 5.0;

	public const double FigureColumnGap = 6.0;

	public const double ContentPadRight = 9.0;

	public const double NameRunSpacing = 5.0;

	public const double CpGlyphWidth = 6.0;

	public const double CpGlyphAdvance = 4.0;

	public const double MinNameRoomEm = 5.0;

	public const double WholeNameSlack = 1.0;

	public const double GlobeWidth = 12.0;

	public const string MitReserveText = "Mit 100%";

	private const char NameCpSeparator = '\u001f';

	public const uint PanelFill = 1611270672u;

	public const double PanelCornerRadius = 8.0;

	public const uint CardFill = 3641576208u;

	public const double CardCornerRadius = 6.0;

	public const double HeaderSidePad = 6.0;

	public const double HeaderBottomGap = 4.0;

	public const double CardPadLeft = 6.0;

	public const double CardPadTop = 4.0;

	public const double CardPadRight = 9.0;

	public const double CardPadBottom = 5.0;

	public const double CrestWidth = 20.0;

	public const double CrestHeight = 17.0;

	public const double CrestTopNudge = 1.0;

	public const double CrestGap = 7.0;

	public const double HeaderLine1TextSize = 10.5;

	public const double HeaderRunSpacing = 5.0;

	public const double PartyGlyphAdvance = 7.0;

	public const double HeaderRightBlockGap = 8.0;

	public const double HeaderLineGap = 1.0;

	public const double BossNameTextSize = 13.0;

	public const double HpFiguresTextSize = 9.5;

	public const double HpPercentTextSize = 11.5;

	public const double HpPercentGap = 6.0;

	public const double HpBarTopGap = 3.0;

	public const double WordmarkTextSize = 9.5;

	public const double WordmarkLetterSpacingEm = 0.08;

	public const double WordmarkPadX = 2.0;

	public const int Line1RunsBeforeWordmark = 8;

	public const int Line1RunsBeforeMode = 6;

	public const int Line1Separators = 3;

	public const uint Crest = 4292883775u;

	public const uint CrestShade = 4287502368u;

	public const uint HeaderMuted = 3019898879u;

	public const uint HeaderSeparator = 1308622847u;

	public const uint HpFill = 4282370950u;

	public const uint HpTrack = 452984831u;

	public const double HeaderMinWidthForMaxHp = 340.0;

	public const double HpBarHeight = 4.0;

	public const uint ChipBackground = 3425308693u;

	public const uint ChipBorder = 3017815359u;

	public const uint SliderThumbHover = 4293943402u;

	public const uint SliderThumbDrag = 4294937234u;

	public const uint SliderFill = 2866820415u;

	public const uint SliderTrackBorder = 1440757055u;

	public override OverlayThemeId Id => OverlayThemeId.Legion;

	public override uint FallbackClassArgb => 4289876058u;

	public override OverlayThemeMetrics Metrics(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => new OverlayThemeMetrics(22.0, 3.0, 17.0, 12.0, 12.5, 10.5, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: true, 6.0, 0.0, 6.0, 6.0, ClipsRow: true, 400, 0.2, 0.75, 0.2, 1.0), 
			OverlayDensity.Micro => new OverlayThemeMetrics(16.0, 2.0, 0.0, 11.0, 11.5, 10.0, ShowSubLine: false, ShowDamage: false, ShowIcon: false, MetricsHorizontal: true, 6.0, 0.0, 6.0, 6.0, ClipsRow: true, 400, 0.2, 0.75, 0.2, 1.0), 
			_ => new OverlayThemeMetrics(26.0, 3.0, 20.0, 13.0, 13.5, 11.0, ShowSubLine: false, ShowDamage: true, ShowIcon: true, MetricsHorizontal: true, 6.0, 0.0, 6.0, 6.0, ClipsRow: true, 400, 0.2, 0.75, 0.2, 1.0), 
		};
	}

	public static double NameOutlineThickness(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 1.25, 
			OverlayDensity.Micro => 1.0, 
			_ => 2.0, 
		};
	}

	public static bool CjkNameInked(OverlayDensity density)
	{
		bool flag = (uint)(density - 1) <= 1u;
		return !flag;
	}

	public static double CjkNameOutlineThickness(OverlayDensity density)
	{
		if (!CjkNameInked(density))
		{
			return 0.0;
		}
		return NameOutlineThickness(density);
	}

	public static double NameOutlineThickness(OverlayDensity density, string? name)
	{
		if (!ContainsCjk(name))
		{
			return NameOutlineThickness(density);
		}
		return CjkNameOutlineThickness(density);
	}

	public static bool ContainsCjk(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		int num = 0;
		while (num < text.Length)
		{
			char c = text[num];
			if (c >= '\u3130')
			{
				if (c >= '一')
				{
					if (c >= '가')
					{
						if (c <= '\ud7af')
						{
							goto IL_0085;
						}
					}
					else if (c <= '鿿')
					{
						goto IL_0085;
					}
				}
				else if (c >= '㐀')
				{
					if (c <= '䶿')
					{
						goto IL_0085;
					}
				}
				else if (c <= '\u318f')
				{
					goto IL_0085;
				}
			}
			else if (c >= '\u3040')
			{
				if (c <= 'ヿ')
				{
					goto IL_0085;
				}
			}
			else if (c >= 'ᄀ' && c <= 'ᇿ')
			{
				goto IL_0085;
			}
			bool flag = false;
			goto IL_008b;
			IL_008b:
			if (flag)
			{
				return true;
			}
			num++;
			continue;
			IL_0085:
			flag = true;
			goto IL_008b;
		}
		return false;
	}

	public static uint FigureShadow(OverlayDensity density)
	{
		if (density != OverlayDensity.Micro)
		{
			return 3640655872u;
		}
		return 1929379840u;
	}

	public static double MitOutlineThickness(OverlayDensity density)
	{
		if ((uint)(density - 1) <= 1u)
		{
			return 0.0;
		}
		return 1.5;
	}

	public static double NoIconLeadWidth(double rowHeight)
	{
		double num = Math.Round(0.85 * PillRadius(rowHeight));
		return Math.Max(0.0, num - 4.0 - 2.0);
	}

	public static double NameReserve(double nameFontSize)
	{
		return 2.0 + 5.0 * Math.Max(0.0, nameFontSize);
	}

	public static double NameReserve(double nameFontSize, double nameWidth)
	{
		double num = NameReserve(nameFontSize);
		if (!double.IsFinite(nameWidth) || nameWidth <= 0.0)
		{
			return num;
		}
		return Math.Min(num, 2.0 + nameWidth + 1.0);
	}

	public static double CpSlotWidth(double cpFigureWidth)
	{
		return 14.0 + Math.Max(0.0, cpFigureWidth);
	}

	public static NameLineExtras NameLine(double listNameCellWidth, double nameFontSize, bool densityShowsCp, IEnumerable<NameLineRow> rows, double mitWidth)
	{
		if (!double.IsFinite(listNameCellWidth) || !double.IsFinite(nameFontSize) || nameFontSize <= 0.0 || !double.IsFinite(mitWidth) || mitWidth < 0.0)
		{
			return default;
		}
		bool flag = false;
		bool flag2 = false;
		double num = 0.0;
		foreach (NameLineRow row in rows)
		{
			flag2 = true;
			bool flag3 = densityShowsCp && double.IsFinite(row.CpWidth) && row.CpWidth > 0.0;
			flag |= flag3;
			num = Math.Max(num, NameReserve(nameFontSize, row.NameWidth) + (flag3 ? CpSlotWidth(row.CpWidth) : 0.0));
		}
		if (!flag2)
		{
			num = NameReserve(nameFontSize);
		}
		double num2 = num + 5.0 + mitWidth;
		double num3 = num2 + 5.0 + 12.0;
		return new NameLineExtras(flag && listNameCellWidth >= num, listNameCellWidth >= num2, listNameCellWidth >= num3);
	}

	public static NameLineExtras NameLine(double listNameCellWidth, double nameFontSize, bool densityShowsCp, double widestCpFigureWidth, double mitWidth, double widestNameWidth)
	{
		if (!double.IsFinite(widestCpFigureWidth))
		{
			return default;
		}
		return NameLine(listNameCellWidth, nameFontSize, densityShowsCp, new NameLineRow[1]
		{
			new NameLineRow(widestNameWidth, widestCpFigureWidth)
		}, mitWidth);
	}

	public static double ListNameCellWidth(double leadAndNameCellWidth, bool listShowsIcons, double iconSize, double noIconLead)
	{
		if (!double.IsFinite(leadAndNameCellWidth) || !double.IsFinite(iconSize) || !double.IsFinite(noIconLead))
		{
			return double.NaN;
		}
		return leadAndNameCellWidth - (listShowsIcons ? (Math.Max(0.0, iconSize) + 5.0) : Math.Max(0.0, noIconLead));
	}

	public static string JoinNameLines(IEnumerable<(string Name, string Cp)> rows)
	{
		return string.Join("\n", rows.Select(((string Name, string Cp) r) => Clean(r.Name) + "\u001f" + Clean(r.Cp)));
	}

	public static IReadOnlyList<(string Name, string Cp)> SplitNameLines(string? joined)
	{
		if (string.IsNullOrEmpty(joined))
		{
			return Array.Empty<(string, string)>();
		}
		List<(string, string)> list = new List<(string, string)>();
		string[] array = joined.Split('\n');
		foreach (string text in array)
		{
			int num = text.IndexOf('\u001f');
			string text2 = ((num < 0) ? text : text.Substring(0, num));
			string text4;
			if (num >= 0)
			{
				string text3 = text;
				int num2 = num + 1;
				text4 = text3.Substring(num2, text3.Length - num2);
			}
			else
			{
				text4 = string.Empty;
			}
			string text5 = text4;
			if (text2.Length > 0 || text5.Length > 0)
			{
				list.Add((text2, text5));
			}
		}
		return list;
	}

	private static string Clean(string? text)
	{
		return (text ?? string.Empty).Replace('\n', ' ').Replace('\u001f', ' ');
	}

	public static double PillRadius(double rowHeight)
	{
		if (!double.IsFinite(rowHeight) || !(rowHeight > 0.0))
		{
			return 0.0;
		}
		return rowHeight / 2.0;
	}

	public static double PillWidth(double fraction, double trackWidth, double rowHeight)
	{
		if (!double.IsFinite(trackWidth) || trackWidth <= 0.0)
		{
			return 0.0;
		}
		double num = (double.IsFinite(fraction) ? Math.Clamp(fraction, 0.0, 1.0) : 0.0);
		double val = ((double.IsFinite(rowHeight) && rowHeight > 0.0) ? rowHeight : 0.0);
		return Math.Min(trackWidth, Math.Max(num * trackWidth, val));
	}

	public static bool ShowsCombatPower(OverlayDensity density)
	{
		return density != OverlayDensity.Micro;
	}

	public static double DpsColumnWidth(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 60, 
			OverlayDensity.Micro => 54, 
			_ => 66, 
		};
	}

	public static double ShareColumnWidth(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 38, 
			OverlayDensity.Micro => 36, 
			_ => 41, 
		};
	}

	public static double DamageColumnWidth(OverlayDensity density)
	{
		return density switch
		{
			OverlayDensity.Compact => 44, 
			OverlayDensity.Micro => 40, 
			_ => 48, 
		};
	}

	public static LegionRowColors RowColors(uint classArgb, bool isRankOne, bool isYou)
	{
		uint argb = ThemeColorMath.WithAlpha(classArgb, byte.MaxValue);
		double num = (isRankOne ? 0.06 : 0.0);
		return new LegionRowColors(ThemeColorMath.WithAlpha(ThemeColorMath.MixBlack(argb, 0.56 + num), 235), ThemeColorMath.WithAlpha(ThemeColorMath.MixBlack(argb, 0.44 + num), 235), isYou ? 3439329279u : 0u);
	}

	public static bool ToneNeedsOutline(ContributionTone tone)
	{
		if ((uint)(tone - 1) <= 1u)
		{
			return true;
		}
		return false;
	}

	public static double ShareOutlineThickness(ContributionTone tone, OverlayDensity density)
	{
		if (!ToneNeedsOutline(tone))
		{
			return 0.0;
		}
		return ToneOutlineThickness(density);
	}

	public static double ToneOutlineThickness(OverlayDensity density)
	{
		if ((uint)(density - 1) > 1u)
		{
			return 1.5;
		}
		return 1.0;
	}

	public static bool ModeFits(double line1CellWidth, double textRunsWidth, double modeWidth)
	{
		if (!double.IsFinite(line1CellWidth) || !double.IsFinite(textRunsWidth) || !double.IsFinite(modeWidth) || textRunsWidth < 0.0 || modeWidth < 0.0)
		{
			return false;
		}
		return textRunsWidth + 7.0 + 30.0 + modeWidth <= line1CellWidth;
	}

	public static bool WordmarkFits(double line1CellWidth, double textRunsWidth, double wordmarkWidth)
	{
		if (!double.IsFinite(line1CellWidth) || !double.IsFinite(textRunsWidth) || !double.IsFinite(wordmarkWidth) || textRunsWidth < 0.0 || wordmarkWidth < 0.0)
		{
			return false;
		}
		return textRunsWidth + 7.0 + 40.0 + wordmarkWidth <= line1CellWidth;
	}

	public static double WordmarkWidth(double lettersWidth, int letterCount)
	{
		return lettersWidth + (double)Math.Max(0, letterCount) * 0.08 * 9.5 + 4.0;
	}

	public static string BossHpFigures(string? current, string? max, double headerWidth, IFormatProvider? culture = null)
	{
		if (current == null)
		{
			current = string.Empty;
		}
		if (max == null)
		{
			max = string.Empty;
		}
		bool flag = double.IsFinite(headerWidth) && headerWidth >= 340.0;
		string text = (flag ? current : ShortHp(current, culture));
		if (string.IsNullOrWhiteSpace(max))
		{
			return text;
		}
		return text + " / " + (flag ? max : ShortHp(max, culture));
	}

	public static string ShortHp(string? text, IFormatProvider? culture = null)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return text ?? string.Empty;
		}
		long num = 0L;
		int num2 = 0;
		string text2 = text;
		foreach (char c in text2)
		{
			if ((c >= '0' && c <= '9') || 1 == 0)
			{
				if (++num2 > 18)
				{
					return text;
				}
				num = num * 10 + (c - 48);
			}
		}
		if (num2 == 0)
		{
			return text;
		}
		IFormatProvider formatProvider = culture ?? CultureInfo.CurrentCulture;
		if (num >= 999950)
		{
			if (num >= 999950000)
			{
				return ((double)num / 1000000000.0).ToString("F2", formatProvider) + "B";
			}
			return ((double)num / 1000000.0).ToString("F1", formatProvider) + "M";
		}
		if (num >= 1000)
		{
			return ((double)num / 1000.0).ToString("F1", formatProvider) + "K";
		}
		return num.ToString(formatProvider);
	}
}
