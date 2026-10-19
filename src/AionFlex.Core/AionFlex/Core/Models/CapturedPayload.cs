using System;

namespace AionFlex.Core.Models;

public sealed class CapturedPayload
{
	public int SourcePort { get; init; }

	public int DestinationPort { get; init; }

	public byte[] Data { get; init; } = Array.Empty<byte>();

	public string? DeviceName { get; init; }

	public CapturedPayload(int sourcePort, int destinationPort, byte[] data, string? deviceName = null)
	{
		SourcePort = sourcePort;
		DestinationPort = destinationPort;
		Data = data ?? Array.Empty<byte>();
		DeviceName = deviceName;
	}

	public override string ToString()
	{
		return $"Payload[{SourcePort}->{DestinationPort}, {Data.Length} bytes, Device={DeviceName}]";
	}
}
