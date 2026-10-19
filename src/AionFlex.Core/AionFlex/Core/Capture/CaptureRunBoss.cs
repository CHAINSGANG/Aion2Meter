using System;
using System.Collections.Generic;

namespace AionFlex.Core.Capture;

public sealed class CaptureRunBoss
{
	public int EntityId { get; set; }

	public int NpcId { get; set; }

	public string? Name { get; set; }

	public string? Role { get; set; }

	public long? MaxHp { get; set; }

	public DateTime? FirstHitUtc { get; set; }

	public DateTime? LastHitUtc { get; set; }

	public DateTime? KillUtc { get; set; }

	public List<DateTime> WipesUtc { get; set; } = new List<DateTime>();
}
