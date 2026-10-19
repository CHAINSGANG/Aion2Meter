using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IBuffTracker
{
	void OnAbnormalEventParsed(object? sender, AbnormalEvent e);

	ObservedBuffSnapshot GetSnapshot(int entityId);

	ObservedBuffSnapshot GetSnapshot(int entityId, IReadOnlyCollection<int> abnormalIds);

	ObservedBuffSnapshot GetSnapshot(int entityId, IReadOnlyCollection<int> abnormalIds, long untilMs);

	IReadOnlyList<OpenBuffWindow> GetOpenBuffs(int entityId);

	void ResetFightScope();

	void Clear();
}
