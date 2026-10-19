using System.Threading;
using System.Threading.Tasks;

namespace AionFlex.Core.Interfaces;

public interface IProfileUploadService
{
	Task<bool> EnqueueAsync<TPayload>(TPayload payload, CancellationToken ct = default(CancellationToken)) where TPayload : class;

	Task StartAsync(CancellationToken ct);

	Task StopAsync(CancellationToken ct);
}
