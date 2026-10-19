using System.Collections.Generic;
using System.Threading.Tasks;

namespace AionFlex.Core.Interfaces;

public interface IMobRepository
{
	string? GetMobName(int templateCode);

	uint GetMaxHp(int code);

	string? GetMobRole(int npcId);

	string? GetMobDungeon(int npcId);

	string? GetBossInstanceDungeon(int npcId);

	bool IsInstanceDungeon(string? dungeonKey);

	string? GetMobDifficulty(int npcId);

	string? GetMobTierToken(int npcId);

	int? GetBerserkSeconds(int npcId);

	string? GetDungeonDisplayName(string dungeonKey);

	string? GetDungeonCategory(string? dungeonKey);

	IReadOnlyList<int> GetDungeonBossNpcIds(string dungeonKey);

	uint GetBossHpThreshold(string? dungeonKey);

	bool HasMob(int templateCode);

	string? GetPortraitPath(int npcId);

	bool IsTrainingDummy(int npcId);

	Task ReloadAsync();

	int GetMobCount();
}
