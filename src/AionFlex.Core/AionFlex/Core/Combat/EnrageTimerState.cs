namespace AionFlex.Core.Combat;

public readonly record struct EnrageTimerState(EnrageTimerKind Kind, long EnrageLeftMs, long? ProjectedKillMs, EnrageVerdict Verdict)
{
	public static EnrageTimerState Hidden => new EnrageTimerState(EnrageTimerKind.Hidden, 0L, null, EnrageVerdict.None);

	public static EnrageTimerState Enraged => new EnrageTimerState(EnrageTimerKind.Enraged, 0L, null, EnrageVerdict.None);
}
