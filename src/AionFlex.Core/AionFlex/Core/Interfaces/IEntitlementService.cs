using System;
using System.Threading;
using System.Threading.Tasks;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IEntitlementService
{
	EntitlementSnapshot Current { get; }

	event EventHandler<EntitlementSnapshot>? SnapshotChanged;

	Task<EntitlementSnapshot> RefreshAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<(bool Success, string Message)> RequestDeviceMoveAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<(bool Success, string Message)> ReleaseDeviceAsync(string code, CancellationToken cancellationToken = default(CancellationToken));

	void SignOut();
}
