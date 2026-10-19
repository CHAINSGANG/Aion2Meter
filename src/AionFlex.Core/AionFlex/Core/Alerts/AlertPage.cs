namespace AionFlex.Core.Alerts;

public sealed record AlertPage(string Key, AlertSoundKind Kind, string Title, string Lede, string Glyph, string GradientFrom, string GradientTo, string CountdownText, string StartedText, string StartedCountdown, bool IsPro, string? StartedGlyph = null, string? StartedFrom = null, string? StartedTo = null)
{
	public string StartedIcon => StartedGlyph ?? Glyph;

	public string StartedGradientFrom => StartedFrom ?? GradientFrom;

	public string StartedGradientTo => StartedTo ?? GradientTo;
}
