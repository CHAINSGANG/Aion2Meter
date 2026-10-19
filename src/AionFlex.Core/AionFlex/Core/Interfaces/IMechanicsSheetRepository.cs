using System.Collections.Generic;
using System.Threading.Tasks;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IMechanicsSheetRepository
{
	IReadOnlyList<BossMechanics> GetAllBosses();

	IReadOnlyList<MechanicsSheet> GetAllSheets();

	Task ReloadAsync();
}
