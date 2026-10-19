using System;
using System.Collections.Generic;

namespace AionFlex.Core.Capture;

public sealed class CaptureRunSavedSession
{
	public string File { get; set; } = "";

	public string? SessionId { get; set; }

	public string? BossName { get; set; }

	public int BossEntityId { get; set; }

	public int BossNpcId { get; set; }

	public DateTime? StartedUtc { get; set; }

	public DateTime? EndedUtc { get; set; }

	public List<string> Players { get; set; } = new List<string>();
}
