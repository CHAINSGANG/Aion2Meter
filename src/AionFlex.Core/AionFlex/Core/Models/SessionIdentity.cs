namespace AionFlex.Core.Models;

public sealed class SessionIdentity
{
	public string CharacterName { get; init; } = "";

	public int? ServerId { get; init; }

	public string? ServerShortCode { get; init; }

	public string? ServerName { get; init; }

	public string? Region { get; init; }

	public string? Faction { get; init; }

	public uint? CharacterId { get; init; }

	public string? ClientVersion { get; init; }
}
