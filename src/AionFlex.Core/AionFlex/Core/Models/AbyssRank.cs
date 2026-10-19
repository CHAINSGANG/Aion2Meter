using System;
using AionFlex.Core.Localization;

namespace AionFlex.Core.Models;

public static class AbyssRank
{
	public const int BestGrade = 1;

	public const int WorstGrade = 19;

	private static readonly string[] English = new string[19]
	{
		"Supreme Commander", "Commander", "Marshal", "General", "5-Star Officer", "4-Star Officer", "3-Star Officer", "2-Star Officer", "1-Star Officer", "First-Class Soldier",
		"Second-Class Soldier", "Third-Class Soldier", "Fourth-Class Soldier", "Fifth-Class Soldier", "Sixth-Class Soldier", "Seventh-Class Soldier", "Eighth-Class Soldier", "Ninth-Class Soldier", "Trainee"
	};

	private static readonly string[] Korean = new string[19]
	{
		"총사령관", "사령관", "대장군", "장군", "5성장교", "4성장교", "3성장교", "2성장교", "1성장교", "1급병",
		"2급병", "3급병", "4급병", "5급병", "6급병", "7급병", "8급병", "9급병", "훈련병"
	};

	private static readonly string[] Chinese = new string[19]
	{
		"總司令官", "司令官", "大將軍", "將軍", "5星軍官", "4星軍官", "3星軍官", "2星軍官", "1星軍官", "1等兵",
		"2等兵", "3等兵", "4等兵", "5等兵", "6等兵", "7等兵", "8等兵", "9等兵", "訓練兵"
	};

	private static readonly string[] ShortLabels = new string[19]
	{
		"SC", "CMD", "MAR", "GEN", "5★", "4★", "3★", "2★", "1★", "1st",
		"2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "T"
	};

	private static Func<Language>? _languageProvider;

	public const int KillBaseScore = 100;

	public static bool IsValidGrade(int grade)
	{
		if (grade >= 1)
		{
			return grade <= 19;
		}
		return false;
	}

	public static string? GetName(int grade, Language language = Language.English)
	{
		if (!IsValidGrade(grade))
		{
			return null;
		}
		return (language switch
		{
			Language.Korean => Korean, 
			Language.Chinese => Chinese, 
			_ => English, 
		})[grade - 1];
	}

	public static string? GetShortLabel(int grade)
	{
		if (!IsValidGrade(grade))
		{
			return null;
		}
		return ShortLabels[grade - 1];
	}

	public static void SetLanguageProvider(Func<Language> languageProvider)
	{
		_languageProvider = languageProvider;
	}

	public static string? GetLocalizedName(int grade)
	{
		return GetName(grade, _languageProvider?.Invoke() ?? Language.English);
	}

	public static bool IsOfficerOrAbove(int grade)
	{
		if (grade >= 1)
		{
			return grade <= 9;
		}
		return false;
	}

	public static int GetPrestigeWeight(int grade)
	{
		if (!IsValidGrade(grade))
		{
			return 0;
		}
		return (int)Math.Round((double)(19 - grade) * 100.0 / 18.0);
	}

	public static bool TryGetKillScore(int? opponentGrade, out int score)
	{
		if (opponentGrade.HasValue)
		{
			int valueOrDefault = opponentGrade.GetValueOrDefault();
			if (IsValidGrade(valueOrDefault))
			{
				score = 100 + GetPrestigeWeight(valueOrDefault);
				return true;
			}
		}
		score = 0;
		return false;
	}
}
