using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IDefenseTracker
{
	void OnDefenseEventParsed(object? sender, DefenseEvent e);

	void OnDamageEventParsed(object? sender, DamageEvent e);

	DefenseSnapshot GetSnapshot(int localPlayerId, int targetId);

	DefenseSnapshot GetSnapshot(int localPlayerId, int targetId, long untilMs);

	void Clear(long keepFromMs = long.MaxValue);
}
