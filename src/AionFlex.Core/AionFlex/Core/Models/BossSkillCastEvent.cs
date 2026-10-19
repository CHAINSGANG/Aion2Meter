using System;

namespace AionFlex.Core.Models;

public sealed class BossSkillCastEvent
{
	public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public int CasterId { get; init; }

	public int SkillCode { get; init; }
}
