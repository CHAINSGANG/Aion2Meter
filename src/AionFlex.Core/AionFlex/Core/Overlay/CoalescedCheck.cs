namespace AionFlex.Core.Overlay;

public sealed class CoalescedCheck
{
	private bool _pending;

	public bool IsPending => _pending;

	public bool Request()
	{
		if (_pending)
		{
			return false;
		}
		_pending = true;
		return true;
	}

	public bool TryBeginRun()
	{
		if (!_pending)
		{
			return false;
		}
		_pending = false;
		return true;
	}

	public void Cancel()
	{
		_pending = false;
	}
}
