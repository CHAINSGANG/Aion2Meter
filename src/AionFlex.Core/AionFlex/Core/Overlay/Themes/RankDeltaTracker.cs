using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Overlay.Themes;

public sealed class RankDeltaTracker<T> where T : class
{
	private sealed class State
	{
		public int Rank;

		public int Delta;

		public long MovedAtMs;
	}

	public const long VisibleMs = 2500L;

	private readonly Dictionary<T, State> _states = new Dictionary<T, State>(ReferenceEqualityComparer.Instance);

	public int Observe(T row, int rank, long nowMs)
	{
		if (!_states.TryGetValue(row, out State value))
		{
			_states[row] = new State
			{
				Rank = rank
			};
			return 0;
		}
		if (rank != value.Rank)
		{
			value.Delta = value.Rank - rank;
			value.MovedAtMs = nowMs;
			value.Rank = rank;
		}
		if (value.Delta == 0 || nowMs - value.MovedAtMs > 2500)
		{
			return 0;
		}
		return value.Delta;
	}

	public void Prune(IEnumerable<T> alive)
	{
		HashSet<T> hashSet = new HashSet<T>(alive, ReferenceEqualityComparer.Instance);
		foreach (T item in _states.Keys.ToList())
		{
			if (!hashSet.Contains(item))
			{
				_states.Remove(item);
			}
		}
	}

	public void Reset()
	{
		_states.Clear();
	}
}
