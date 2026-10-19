using System.Collections.Generic;
using System.Threading.Tasks;

namespace AionFlex.Core.Interfaces;

public interface ISkillDetailRepository
{
	string? GetDescription(int baseCode);

	string? GetTags(int baseCode);

	IReadOnlyList<string> GetSpecDescriptions(int baseCode);

	bool HasDetails(int baseCode);

	Task ReloadAsync();
}
