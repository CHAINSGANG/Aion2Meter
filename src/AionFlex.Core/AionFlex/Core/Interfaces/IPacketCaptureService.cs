using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IPacketCaptureService : IDisposable
{
	bool IsRunning { get; }

	long TcpRetransmissions { get; }

	long TcpGaps { get; }

	long TcpGapBytes { get; }

	event EventHandler<CapturedPayload>? PayloadCaptured;

	void Start();

	void Stop();

	(long Received, long Dropped) GetCaptureStatistics();
}
