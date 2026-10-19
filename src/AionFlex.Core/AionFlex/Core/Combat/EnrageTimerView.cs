namespace AionFlex.Core.Combat;

public readonly record struct EnrageTimerView(bool Visible, bool IsLocked, string Text, EnrageTimerTone Tone)
{
	public static EnrageTimerView Hidden => new EnrageTimerView(Visible: false, IsLocked: false, "", EnrageTimerTone.Neutral);
}
