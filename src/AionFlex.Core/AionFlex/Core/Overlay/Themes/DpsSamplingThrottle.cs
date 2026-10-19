namespace AionFlex.Core.Overlay.Themes;

public sealed class DpsSamplingThrottle
{
	public const long IntervalMs = 250L;

	private long? _lastMs;

	public bool ShouldSample(long nowMs)
	{
		long? lastMs = _lastMs;
		if (lastMs.HasValue)
		{
			long valueOrDefault = lastMs.GetValueOrDefault();
			if (nowMs >= valueOrDefault && nowMs - valueOrDefault < 250)
			{
				return false;
			}
		}
		_lastMs = nowMs;
		return true;
	}

	public void Reset()
	{
		_lastMs = null;
	}
}
