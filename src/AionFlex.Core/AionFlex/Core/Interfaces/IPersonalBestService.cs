using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IPersonalBestService
{
	PersonalBestRecord? GetBest(string characterName, string bossName);

	IReadOnlyList<PersonalBestRecord> GetAllForCharacter(string characterName);

	bool TryRecordBest(string characterName, string bossName, string instanceName, double dps, long totalDamage, string battleTime);

	bool TryRecordBest(string characterName, string bossName, string instanceName, double dps, long totalDamage, string battleTime, out double? previousDps);
}
