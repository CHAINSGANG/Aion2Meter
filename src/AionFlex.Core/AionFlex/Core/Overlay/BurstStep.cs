namespace AionFlex.Core.Overlay;

public readonly record struct BurstStep(int Attempt, int? NextDelayMs)
{
	public static BurstStep Idle => new BurstStep(0, null);

	public bool RaiseNow => Attempt > 0;
}
