using System;
using System.Threading.Tasks;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IUpdateService : IDisposable
{
	Task<UpdateCheckResult> CheckForUpdateAsync();

	Task ApplyUpdateAsync(UpdateCheckResult update, IProgress<UpdateProgress>? progress = null);

	(string? Version, DateTime? AtUtc) GetLastAutoInstall();

	void RecordAutoInstall(string version);
}
