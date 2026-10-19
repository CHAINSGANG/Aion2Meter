using System;

namespace AionFlex.Core.Models;

public sealed class GameEvent
{
	public string Id { get; init; } = "";

	public string Name => Names.En;

	public EventText Names { get; init; } = EventText.Same("");

	public GameEventType Type { get; init; }

	public int? NpcId { get; init; }

	public TimeOnly[] ServerTimes { get; init; } = Array.Empty<TimeOnly>();

	public DayOfWeek[]? DaysOfWeek { get; init; }

	public TimeSpan Duration { get; init; } = TimeSpan.Zero;

	public EventText? Locations { get; init; }

	public string? Location => Locations?.En;
}
