using System.Collections.Concurrent;

namespace AionFlex.Core.Combat;

public sealed class SavedBossLives
{
	private readonly ConcurrentDictionary<int, int> _savedGeneration = new ConcurrentDictionary<int, int>();

	public void MarkSaved(int entityId, int lifeGeneration)
	{
		_savedGeneration[entityId] = lifeGeneration;
	}

	public bool IsSaved(int entityId, int lifeGeneration)
	{
		if (_savedGeneration.TryGetValue(entityId, out var value))
		{
			return value == lifeGeneration;
		}
		return false;
	}

	public void Forget(int entityId)
	{
		_savedGeneration.TryRemove(entityId, out var _);
	}

	public void Clear()
	{
		_savedGeneration.Clear();
	}
}
