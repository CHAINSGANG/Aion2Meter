using System.Collections.Generic;
using AionFlex.Core.Localization;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IProcBuffRepository
{
	Language DisplayLanguage { get; }

	IReadOnlyList<ProcBuffSpec> GetForJob(JobClass job);

	IReadOnlyList<ProcBuffSpec> GetAll();

	IReadOnlyCollection<int> GetBuffAbnormalsForJob(JobClass job);
}
