namespace AionFlex.Core.Combat;

public static class EnrageTimerPresenter
{
	public const string LockedText = "Enrage timer · Pro";

	public const string EnragedText = "ENRAGED";

	private const long MaxShownKillMs = 3600000L;

	public static EnrageTimerView Present(EnrageTimerState state, bool entitled)
	{
		if (state.Kind == EnrageTimerKind.Hidden)
		{
			return EnrageTimerView.Hidden;
		}
		if (!entitled)
		{
			return new EnrageTimerView(Visible: true, IsLocked: true, "Enrage timer · Pro", EnrageTimerTone.Locked);
		}
		if (state.Kind == EnrageTimerKind.Enraged)
		{
			return new EnrageTimerView(Visible: true, IsLocked: false, "ENRAGED", EnrageTimerTone.Enraged);
		}
		string text = "Enrage " + MinutesSeconds((state.EnrageLeftMs + 999) / 1000);
		long? projectedKillMs = state.ProjectedKillMs;
		if (projectedKillMs.HasValue)
		{
			long valueOrDefault = projectedKillMs.GetValueOrDefault();
			text += ((valueOrDefault > 3600000) ? " · kill 60:00+" : (" · kill ~" + MinutesSeconds((valueOrDefault + 500) / 1000)));
			return new EnrageTimerView(Visible: true, IsLocked: false, text, state.Verdict switch
			{
				EnrageVerdict.OnTrack => EnrageTimerTone.OnTrack, 
				EnrageVerdict.Tight => EnrageTimerTone.Tight, 
				EnrageVerdict.TooSlow => EnrageTimerTone.TooSlow, 
				_ => EnrageTimerTone.Neutral, 
			});
		}
		return new EnrageTimerView(Visible: true, IsLocked: false, text, EnrageTimerTone.Neutral);
	}

	private static string MinutesSeconds(long seconds)
	{
		return $"{seconds / 60}:{seconds % 60:00}";
	}
}
