using System;

namespace AionFlex.Core.Models;

public sealed class RiftOccurrence
{
	public DateTime LocalStart { get; init; }

	public DateTime LocalEnd { get; init; }

	public bool IsActive { get; init; }

	public bool IsNext { get; init; }

	public bool IsPast { get; init; }

	public TimeSpan TimeUntilStart { get; init; }

	public string LocalTimeText => LocalStart.ToString("h:mm tt");
}
