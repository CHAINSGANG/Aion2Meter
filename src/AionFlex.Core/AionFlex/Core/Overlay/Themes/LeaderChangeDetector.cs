namespace AionFlex.Core.Overlay.Themes;

public sealed class LeaderChangeDetector<T> where T : class
{
	private T? _previous;

	public T? Observe(T? currentLeader)
	{
		T previous = _previous;
		_previous = currentLeader;
		if (previous == null || currentLeader == null || previous == currentLeader)
		{
			return null;
		}
		return currentLeader;
	}

	public void Reset()
	{
		_previous = null;
	}
}
