namespace AionFlex.Core.Interfaces;

public interface IInstanceRepository
{
	bool IsInstanceZone(int zoneId);

	string? GetDungeonKey(int zoneId);

	string? GetDungeonDisplayName(int zoneId);
}
