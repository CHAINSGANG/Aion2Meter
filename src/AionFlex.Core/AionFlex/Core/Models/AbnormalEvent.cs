using System;

namespace AionFlex.Core.Models;

public sealed class AbnormalEvent
{
	public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public int TargetId { get; init; }

	public int Slot { get; init; }

	public int AbnormalId { get; init; }

	public uint DurationMs { get; init; }

	public ulong ServerStampMs { get; init; }

	public int CasterId { get; init; }

	public int SourceSkillId { get; init; }

	public bool IsRefresh { get; init; }

	public bool IsRemove { get; init; }
}
