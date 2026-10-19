using System;

namespace AionFlex.Core.Models;

public sealed class DefenseEvent
{
	public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public int ActorId { get; init; }

	public int TargetId { get; init; }

	public int SkillCode { get; init; }

	public DefenseOutcome Outcome { get; init; }

	public int EventCounter { get; init; }

	public int Damage { get; init; }

	public int FlagByte { get; init; }
}
