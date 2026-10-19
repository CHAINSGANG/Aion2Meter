namespace AionFlex.Core.Models;

public sealed record OpenBuffWindow(int AbnormalId, long StartMs, uint DurationMs, int CasterId, int TargetId, int Slot, bool OpenedByRefresh = false, long? ServerExpiryMs = null, int SourceSkillId = 0)
{
	public bool HasTimer
	{
		get
		{
			if (DurationMs != 0)
			{
				return DurationMs != uint.MaxValue;
			}
			return false;
		}
	}

	public long? EndMs
	{
		get
		{
			if (!HasTimer)
			{
				return null;
			}
			return StartMs + DurationMs;
		}
	}
}
