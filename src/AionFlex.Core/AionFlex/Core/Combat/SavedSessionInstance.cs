using AionFlex.Core.Interfaces;

namespace AionFlex.Core.Combat;

public static class SavedSessionInstance
{
	public static (string? DungeonKey, string InstanceName) Resolve(IMobRepository mobs, int bossNpcId, bool isConfirmedBoss, string? contextDungeonKey, string? contextDifficulty)
	{
		string text = ((bossNpcId > 0) ? mobs.GetBossInstanceDungeon(bossNpcId) : null) ?? contextDungeonKey;
		string text2 = ((text != null) ? (mobs.GetDungeonDisplayName(text) ?? text) : "Unknown");
		string text3 = (isConfirmedBoss ? mobs.GetMobDifficulty(bossNpcId) : null) ?? contextDifficulty;
		return (DungeonKey: text, InstanceName: (text3 != null) ? (text2 + " (" + text3 + ")") : text2);
	}
}
