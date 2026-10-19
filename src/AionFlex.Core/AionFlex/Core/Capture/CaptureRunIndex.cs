using System;
using System.Collections.Generic;

namespace AionFlex.Core.Capture;

public sealed class CaptureRunIndex
{
	public const int CurrentSchema = 1;

	public int Schema { get; set; } = 1;

	public string Name { get; set; } = "";

	public string? AppVersion { get; set; }

	public string CaptureFile { get; set; } = "";

	public string? LogFile { get; set; }

	public string? SourceSessionCapture { get; set; }

	public string? SourceSessionLog { get; set; }

	public DateTime FileStartUtc { get; set; }

	public DateTime FileEndUtc { get; set; }

	public DateTime AnchorUtc { get; set; }

	public string AnchorKind { get; set; } = "";

	public DateTime CloseUtc { get; set; }

	public string CloseKind { get; set; } = "";

	public int? ServerId { get; set; }

	public int? HomeServerId { get; set; }

	public int? DungeonId { get; set; }

	public string? DungeonKey { get; set; }

	public string? InstanceName { get; set; }

	public string? Difficulty { get; set; }

	public DateTime? LiveStartUtc { get; set; }

	public DateTime? EndedUtc { get; set; }

	public DateTime? ResultUtc { get; set; }

	public bool? Cleared { get; set; }

	public CaptureRunPlayer? LocalPlayer { get; set; }

	public List<string> Party { get; set; } = new List<string>();

	public List<CaptureRunBoss> Bosses { get; set; } = new List<CaptureRunBoss>();

	public List<CaptureRunSavedSession> SavedSessions { get; set; } = new List<CaptureRunSavedSession>();
}
