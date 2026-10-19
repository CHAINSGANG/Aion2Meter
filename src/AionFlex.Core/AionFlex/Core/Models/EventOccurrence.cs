using System;

namespace AionFlex.Core.Models;

public sealed class EventOccurrence
{
	public string EventId { get; init; } = "";

	public string Name { get; init; } = "";

	public EventText Names { get; init; } = EventText.Same("");

	public GameEventType Type { get; init; }

	public int? NpcId { get; init; }

	public DateTime LocalStart { get; init; }

	public DateTime LocalEnd { get; init; }

	public bool IsActive { get; init; }

	public bool IsNext { get; init; }

	public bool IsPast { get; init; }

	public TimeSpan TimeUntilStart { get; init; }

	public string? Location { get; init; }

	public EventText? Locations { get; init; }

	public string? Description { get; init; }

	public string LocalTimeText => LocalStart.ToString("h:mm tt");

	public string DayLabel => LocalStart.ToString("ddd");
}
