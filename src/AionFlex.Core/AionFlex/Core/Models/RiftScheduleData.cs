using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class RiftScheduleData
{
	public GameRegion Region { get; init; }

	public string RegionDisplayName { get; init; } = "";

	public string TimeZoneId { get; init; } = "";

	public IReadOnlyList<RiftTimeSlot> TimeSlots { get; init; } = Array.Empty<RiftTimeSlot>();

	public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(30.0);

	public int Capacity { get; init; }

	public string? UnavailableReason { get; init; }
}
