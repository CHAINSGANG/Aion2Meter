using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Mechanics;

public static class MechanicAlertPolicy
{
	public readonly record struct Alert(BossMechanic Mechanic, bool IsBroken);

	public readonly record struct Signals(int BossNpcId, bool Dead, bool GuardDraining, double GuardFraction, IReadOnlyCollection<int> CastSkillCodes);

	public const double GuardBrokenFraction = 0.05;

	public const int FireBannerMs = 8000;

	public const int BrokenBannerMs = 3000;

	public static IReadOnlyList<Alert> Evaluate(IReadOnlyList<BossMechanic> mechanics, ISet<string> firedKeys, Signals signals)
	{
		List<Alert> list = new List<Alert>();
		if (signals.Dead)
		{
			return list;
		}
		foreach (BossMechanic mechanic in mechanics)
		{
			MechanicLiveTrigger liveTrigger = mechanic.LiveTrigger;
			if (liveTrigger != null)
			{
				IReadOnlyList<int> npcIds = liveTrigger.NpcIds;
				if (npcIds != null && npcIds.Count > 0 && !liveTrigger.NpcIds.Contains(signals.BossNpcId))
				{
					continue;
				}
			}
			switch (mechanic.LiveTrigger?.Kind)
			{
			case MechanicLiveTriggerKind.SkillCast:
			{
				IReadOnlyCollection<int> castSkillCodes = signals.CastSkillCodes;
				if (castSkillCodes != null && castSkillCodes.Count > 0 && mechanic.LiveTrigger.SkillCodes.Any(((IEnumerable<int>)castSkillCodes).Contains<int>) && firedKeys.Add(FireKey(mechanic)))
				{
					list.Add(new Alert(mechanic, IsBroken: false));
				}
				break;
			}
			case MechanicLiveTriggerKind.Groggy:
				if (signals.GuardDraining)
				{
					if (firedKeys.Add(FireKey(mechanic)))
					{
						list.Add(new Alert(mechanic, IsBroken: false));
					}
					if (signals.GuardFraction <= 0.05 && firedKeys.Add(BrokenKey(mechanic)))
					{
						list.Add(new Alert(mechanic, IsBroken: true));
					}
				}
				break;
			}
		}
		return list;
	}

	public static string FireKey(BossMechanic mechanic)
	{
		return mechanic.Id + ":fire";
	}

	public static string BrokenKey(BossMechanic mechanic)
	{
		return mechanic.Id + ":broken";
	}

	public static string Title(Alert alert, Language language)
	{
		if (alert.IsBroken)
		{
			return language switch
			{
				Language.Korean => "무력화 성공 ✓", 
				Language.Chinese => "削韌成功 ✓", 
				_ => "Stagger broken ✓", 
			};
		}
		return alert.Mechanic.Action.Get(language);
	}

	public static string Detail(Alert alert, Language language)
	{
		string text;
		if (!alert.IsBroken)
		{
			text = alert.Mechanic.Detail?.Get(language);
			if (text == null)
			{
				return string.Empty;
			}
		}
		else
		{
			text = string.Empty;
		}
		return text;
	}

	public static int BannerMs(Alert alert)
	{
		if (!alert.IsBroken)
		{
			return 8000;
		}
		return 3000;
	}
}
