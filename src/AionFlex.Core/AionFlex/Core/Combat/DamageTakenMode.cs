using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Combat;

public static class DamageTakenMode
{
	public const string PillTooltip = "Damage taken: who is hitting you and with what";

	public const string ModeLabel = "Taken";

	public const string CopyHeader = "=== Damage Taken ===";

	public const string ModeStripTitle = "DAMAGE TAKEN";

	public const string ModeStripHint = "hits on you, not your damage";

	public const string ModeStripExit = "Show damage";

	public static bool IsAvailable(Func<Feature, bool> isEnabled)
	{
		return isEnabled(Feature.DamageTaken);
	}

	public static bool ShowsPill(bool available, bool pvpMode)
	{
		if (available)
		{
			return !pvpMode;
		}
		return false;
	}
}
