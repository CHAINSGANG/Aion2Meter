using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class HealingPolicy
{
	public const string ModeLabel = "Healing (Beta)";

	public const string ModeStripTitle = "HEALING METER";

	public const string ModeStripHint = "heals, not damage";

	public const string ModeStripExit = "Show damage";

	public const string BetaNote = "Healing meter (Beta): healers by heal per second over the current fight. Amounts include overheal, and heal skill variants the meter has not proven yet (e.g. one Chanter Healing Touch variant) are not counted.";

	public static readonly IReadOnlySet<int> ProvenDirectHealCodes = new HashSet<int>
	{
		11260030, 11260230, 12260030, 13260030, 13260350, 14260030, 14260130, 14260230, 14260340, 14260350,
		16200230, 16200350, 17240030, 17240340, 18200030, 18200340, 11720007, 11730008, 12320047, 12720001,
		13710007, 13790007, 14790007, 15790007, 16790001, 14710007, 16120000, 16120030, 16120230, 16120250,
		16120340, 16122350, 16190010, 16190020, 16190040, 16750001, 16770001, 17100000, 17100010, 17100120,
		17100130, 17100150, 17100230, 17100350, 17120000, 17120010, 17120020, 17120120, 17120130, 17120140,
		17120230, 17120240, 17120340, 17120350, 17121230, 17121250, 17121340, 17121350, 17121450, 17160006,
		17160016, 17160026, 17160036, 17160037, 17160046, 17160056, 17160058, 17290000, 17290010, 17290020,
		17720001, 17800001, 18120000, 18120010, 18120020, 18120030, 18120120, 18120130, 18120140, 18120150,
		18120230, 18120250, 18120350, 18121240, 18121350, 18121450, 18160002, 18160032, 18170010, 18170020,
		18240041, 18720001
	};

	public const int LightOfRegenerationBase = 17090000;

	public const int RecuperationBase = 18120000;

	public static bool IsCountedDirectHeal(DamageEvent e)
	{
		if (e.IsHeal && e.Damage > 0)
		{
			return ProvenDirectHealCodes.Contains(e.SkillCode);
		}
		return false;
	}

	public static bool IsProvenHealOverTimeSkill(int skillCode)
	{
		int num = SkillCodeNormalizer.Normalize(skillCode);
		if (num != 17090000)
		{
			return num == 18120000;
		}
		return true;
	}
}
