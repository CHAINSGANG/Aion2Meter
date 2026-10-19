using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Mechanics;

public static class MechanicTriggerText
{
	public const string UnlabelledPhase = "PHASE";

	public static string Describe(MechanicTrigger trigger, Language language)
	{
		string text = trigger.Label?.Get(language);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = null;
		}
		if (trigger.Type != MechanicTriggerType.HpPercent)
		{
			return text ?? "PHASE";
		}
		string text2 = $"{trigger.HpPercent:0}% HP";
		if (text != null)
		{
			return text2 + " · " + text;
		}
		return text2;
	}
}
