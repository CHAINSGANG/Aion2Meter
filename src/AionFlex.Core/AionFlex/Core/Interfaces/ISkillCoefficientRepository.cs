using System.Threading.Tasks;

namespace AionFlex.Core.Interfaces;

public interface ISkillCoefficientRepository
{
	(int Coef, int Flat, int Hits)? GetCoefficients(int baseCode, int level);

	bool HasCoefficients(int baseCode);

	int GetMaxLevel(int baseCode);

	int? GetChainRootCode(int baseCode);

	Task ReloadAsync();
}
