using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Localization;

public static class JobClassLocalizer
{
	private static readonly Dictionary<JobClass, Dictionary<Language, string>> Localizations = new Dictionary<JobClass, Dictionary<Language, string>>
	{
		{
			JobClass.Unknown,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Unknown"
				},
				{
					Language.Korean,
					"알 수 없음"
				},
				{
					Language.Chinese,
					"未知"
				}
			}
		},
		{
			JobClass.Gladiator,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Gladiator"
				},
				{
					Language.Korean,
					"검성"
				},
				{
					Language.Chinese,
					"劍星"
				}
			}
		},
		{
			JobClass.Templar,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Templar"
				},
				{
					Language.Korean,
					"수호성"
				},
				{
					Language.Chinese,
					"守護星"
				}
			}
		},
		{
			JobClass.Ranger,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Ranger"
				},
				{
					Language.Korean,
					"궁성"
				},
				{
					Language.Chinese,
					"弓星"
				}
			}
		},
		{
			JobClass.Assassin,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Assassin"
				},
				{
					Language.Korean,
					"살성"
				},
				{
					Language.Chinese,
					"殺星"
				}
			}
		},
		{
			JobClass.Sorcerer,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Sorcerer"
				},
				{
					Language.Korean,
					"마도성"
				},
				{
					Language.Chinese,
					"魔道星"
				}
			}
		},
		{
			JobClass.Cleric,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Cleric"
				},
				{
					Language.Korean,
					"치유성"
				},
				{
					Language.Chinese,
					"治癒星"
				}
			}
		},
		{
			JobClass.Elementalist,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Elementalist"
				},
				{
					Language.Korean,
					"정령성"
				},
				{
					Language.Chinese,
					"精靈星"
				}
			}
		},
		{
			JobClass.Chanter,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Chanter"
				},
				{
					Language.Korean,
					"호법성"
				},
				{
					Language.Chinese,
					"護法星"
				}
			}
		},
		{
			JobClass.Fighter,
			new Dictionary<Language, string>
			{
				{
					Language.English,
					"Brawler"
				},
				{
					Language.Korean,
					"권성"
				},
				{
					Language.Chinese,
					"拳星"
				}
			}
		}
	};

	public static string GetLocalizedName(JobClass jobClass, Language language)
	{
		if (Localizations.TryGetValue(jobClass, out Dictionary<Language, string> value) && value.TryGetValue(language, out var value2))
		{
			return value2;
		}
		return jobClass.ToString();
	}
}
