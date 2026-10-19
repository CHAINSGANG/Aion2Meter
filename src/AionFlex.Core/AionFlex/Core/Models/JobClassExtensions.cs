using System;
using System.Collections.Generic;
using AionFlex.Core.Localization;

namespace AionFlex.Core.Models;

public static class JobClassExtensions
{
	private static Func<Language>? _languageProvider;

	private static readonly Dictionary<int, JobClass> BasicSkillCodeMap = new Dictionary<int, JobClass>
	{
		{
			11020000,
			JobClass.Gladiator
		},
		{
			12010000,
			JobClass.Templar
		},
		{
			14020000,
			JobClass.Ranger
		},
		{
			13010000,
			JobClass.Assassin
		},
		{
			15210000,
			JobClass.Sorcerer
		},
		{
			17010000,
			JobClass.Cleric
		},
		{
			16010000,
			JobClass.Elementalist
		},
		{
			18010000,
			JobClass.Chanter
		},
		{
			19010000,
			JobClass.Fighter
		}
	};

	public static void SetLanguageProvider(Func<Language> languageProvider)
	{
		_languageProvider = languageProvider;
	}

	public static string GetClassName(this JobClass jobClass)
	{
		Language language = _languageProvider?.Invoke() ?? Language.English;
		return JobClassLocalizer.GetLocalizedName(jobClass, language);
	}

	public static JobClass FromSkillCode(int skillCode)
	{
		if (BasicSkillCodeMap.TryGetValue(skillCode, out var value))
		{
			return value;
		}
		return ((skillCode >= 100000000) ? (skillCode / 10000000) : (skillCode / 1000000)) switch
		{
			11 => JobClass.Gladiator, 
			12 => JobClass.Templar, 
			13 => JobClass.Assassin, 
			14 => JobClass.Ranger, 
			15 => JobClass.Sorcerer, 
			16 => JobClass.Elementalist, 
			17 => JobClass.Cleric, 
			18 => JobClass.Chanter, 
			19 => JobClass.Fighter, 
			_ => JobClass.Unknown, 
		};
	}
}
