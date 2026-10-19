using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AionFlex.Core.Armory;

public interface IArmoryClient
{
	Task<string> SearchAsync(string name, string apiRegion, IReadOnlyList<int> pcIds, CancellationToken ct);

	Task<string> GetEquipmentAsync(ArmoryCharacter character, string apiRegion, CancellationToken ct);

	Task<string> GetItemAsync(ArmoryCharacter character, ArmoryItem item, string apiRegion, CancellationToken ct);

	Task<byte[]> DownloadIconAsync(string url, CancellationToken ct);
}
