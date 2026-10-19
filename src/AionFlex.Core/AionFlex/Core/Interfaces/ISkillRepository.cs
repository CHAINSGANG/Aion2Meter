using System.Collections.Generic;
using System.Threading.Tasks;
using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface ISkillRepository
{
	string GetSkillName(int skillCode);

	string GetSkillName(int skillCode, Language language);

	bool HasSkill(int skillCode);

	bool IsProvenNonDamage(int skillCode);

	bool IsProvenHeal(int skillCode)
	{
		return false;
	}

	IReadOnlyCollection<int> GetAllSkillCodes();

	int[] GetSortedSkillCodes();

	Task ReloadAsync();

	int GetSkillCount();

	SkillMetadata? GetSkillMetadata(int skillCode);

	bool IsPlayerSkill(int skillCode);

	bool IsGhostDamageSkill(int skillCode);

	bool IsSummonCarriedSkill(int skillCode);

	SkillRole GetSkillRole(int skillCode);

	bool IsNonDamageSkill(int skillCode);

	bool IsHealSkill(int skillCode);
}
