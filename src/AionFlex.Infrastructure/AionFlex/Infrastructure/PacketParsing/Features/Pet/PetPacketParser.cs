using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.Pet;

public sealed class PetPacketParser : IPetPacketParser, IFeaturePacketParser
{
	private readonly ILogger<PetPacketParser> _logger;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private readonly Dictionary<string, byte[]> _ffffCarryOver = new Dictionary<string, byte[]>();

	private const int MaxFFFFCarryOverBytes = 1048576;

	private readonly LogRateGate _overLimitLog = new LogRateGate();

	private byte[] _decodeScratch = Array.Empty<byte>();

	public event EventHandler<IReadOnlyList<(int PetId, int Level)>>? PetRosterDiscovered;

	public PetPacketParser(ILogger<PetPacketParser> logger, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger;
		_protocolHealth = protocolHealth;
	}

	public void ProcessPayload(CapturedPayload payload)
	{
		try
		{
			byte[] data = payload.Data;
			if (data == null || data.Length < 1)
			{
				return;
			}
			string text = $"{payload.SourcePort}-{payload.DestinationPort}";
			byte[] array;
			if (_ffffCarryOver.TryGetValue(text, out byte[] value) && value.Length != 0)
			{
				array = new byte[value.Length + data.Length];
				Buffer.BlockCopy(value, 0, array, 0, value.Length);
				Buffer.BlockCopy(data, 0, array, value.Length, data.Length);
			}
			else
			{
				array = data;
			}
			if (array.Length < 12)
			{
				return;
			}
			int num = ScanForUnframedBundles(array);
			if (num >= 0)
			{
				int num2 = array.Length - num;
				if (num2 <= 1048576)
				{
					_ffffCarryOver[text] = array[num..];
					return;
				}
				_ffffCarryOver.Remove(text);
				if (_overLimitLog.ShouldLog(text, out var repeats))
				{
					_logger.LogWarning("\ud83d\udc3e PetPacketParser FF FF carry-over OVER-LIMIT: stream={Stream} {Bytes}b > {Cap}b — bundle lost{Repeats}", text, num2, 1048576, _overLimitLog.SummarySuffix(repeats));
				}
			}
			else
			{
				_ffffCarryOver.Remove(text);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "PetPacketParser: error processing payload");
		}
	}

	private int ScanForUnframedBundles(byte[] payload)
	{
		int result = -1;
		int num = 64;
		for (int i = 0; i < payload.Length - 6; i++)
		{
			if (payload[i] != byte.MaxValue || payload[i + 1] != byte.MaxValue)
			{
				continue;
			}
			int num2 = payload[i + 2] | (payload[i + 3] << 8) | (payload[i + 4] << 16) | (payload[i + 5] << 24);
			if (num2 <= 0 || num2 > 1000000)
			{
				continue;
			}
			int num3 = i + 6;
			if (num3 >= payload.Length)
			{
				continue;
			}
			if (num-- <= 0)
			{
				result = i;
				break;
			}
			int num4 = payload.Length - num3;
			int num5 = num2 + 64;
			if (_decodeScratch.Length < num5)
			{
				_decodeScratch = new byte[num5];
			}
			byte[] decodeScratch = _decodeScratch;
			int num6;
			try
			{
				num6 = LZ4Codec.Decode(payload, num3, num4, decodeScratch, 0, num5);
			}
			catch
			{
				num6 = -1;
			}
			if (num6 < num2)
			{
				int item = LenientLz4Decode(payload.AsSpan(num3, num4), decodeScratch, num5).outLen;
				if (item < num2)
				{
					result = i;
					continue;
				}
				num6 = item;
			}
			byte[] subArray = decodeScratch[..num6];
			ScanForPetRoster(subArray);
		}
		return result;
	}

	private bool ScanForPetRoster(byte[] data)
	{
		int num = -1;
		int num2 = 0;
		int num3 = 0;
		int num4 = data.Length;
		while (num3 + 12 <= num4)
		{
			uint num5 = (uint)(data[num3] | (data[num3 + 1] << 8) | (data[num3 + 2] << 16) | (data[num3 + 3] << 24));
			uint num6 = (uint)(data[num3 + 4] | (data[num3 + 5] << 8) | (data[num3 + 6] << 16) | (data[num3 + 7] << 24));
			uint num7 = (uint)(data[num3 + 8] | (data[num3 + 9] << 8) | (data[num3 + 10] << 16) | (data[num3 + 11] << 24));
			if (num5 == num6 && num5 >= 1000 && num5 <= 1500 && num7 <= 5)
			{
				int num8 = num3;
				int num9 = 0;
				while (num3 + 12 <= num4)
				{
					uint num10 = (uint)(data[num3] | (data[num3 + 1] << 8) | (data[num3 + 2] << 16) | (data[num3 + 3] << 24));
					uint num11 = (uint)(data[num3 + 4] | (data[num3 + 5] << 8) | (data[num3 + 6] << 16) | (data[num3 + 7] << 24));
					uint num12 = (uint)(data[num3 + 8] | (data[num3 + 9] << 8) | (data[num3 + 10] << 16) | (data[num3 + 11] << 24));
					if (num10 != num11 || num10 < 1000 || num10 > 1500 || num12 > 5)
					{
						break;
					}
					num3 += 12;
					num9++;
				}
				if (num9 > num2)
				{
					num = num8;
					num2 = num9;
				}
			}
			else
			{
				num3++;
			}
		}
		if (num >= 0 && num2 >= 2 && num2 < 5)
		{
			if (_protocolHealth != null)
			{
				byte[] array = new byte[num2 * 4];
				for (int i = 0; i < num2; i++)
				{
					int srcOffset = num + i * 12;
					Buffer.BlockCopy(data, srcOffset, array, i * 4, 4);
				}
				_protocolHealth.RecordDecodeMiss("pets", array);
			}
			return false;
		}
		if (num < 0 || num2 < 5)
		{
			return false;
		}
		_protocolHealth?.RecordDecodeOk("pets");
		(int, int)[] array2 = new (int, int)[num2];
		for (int j = 0; j < num2; j++)
		{
			int num13 = num + j * 12;
			int item = data[num13] | (data[num13 + 1] << 8) | (data[num13 + 2] << 16) | (data[num13 + 3] << 24);
			int item2 = data[num13 + 8] | (data[num13 + 9] << 8) | (data[num13 + 10] << 16) | (data[num13 + 11] << 24);
			array2[j] = (item, item2);
		}
		_logger.LogInformation("\ud83d\udc3e Pet roster decoded (PetPacketParser): {Count} pets at offset 0x{Offset:X4} (levels: {Levels})", array2.Length, num, string.Join(",", array2.GroupBy(((int PetId, int Level) r) => r.Level).OrderBy((IGrouping<int, (int PetId, int Level)> g) => g.Key).Select((IGrouping<int, (int PetId, int Level)> g) => $"L{g.Key}:{g.Count()}")));
		PetRosterDiscovered?.Invoke(this, array2);
		return true;
	}

	private static (int outLen, int consumedIn) LenientLz4Decode(ReadOnlySpan<byte> comp, byte[] target, int maxOutput)
	{
		int num = 0;
		int num2 = 0;
		while (num < comp.Length)
		{
			if (num2 >= maxOutput)
			{
				return (outLen: num2, consumedIn: num);
			}
			int num3 = comp[num++] & 0xFF;
			int num4 = num3 >> 4;
			int num5 = num3 & 0xF;
			if (num4 == 15)
			{
				while (num < comp.Length)
				{
					int num6 = comp[num++] & 0xFF;
					num4 += num6;
					if (num6 != 255)
					{
						break;
					}
				}
			}
			if (num + num4 > comp.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			if (num2 + num4 > maxOutput)
			{
				return (outLen: num2, consumedIn: num);
			}
			comp.Slice(num, num4).CopyTo(target.AsSpan(num2));
			num2 += num4;
			num += num4;
			if (num >= comp.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			if (num + 2 > comp.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			int num7 = (comp[num] & 0xFF) | ((comp[num + 1] & 0xFF) << 8);
			num += 2;
			if (num7 == 0 || num7 > num2)
			{
				return (outLen: num2, consumedIn: num);
			}
			if (num5 == 15)
			{
				while (num < comp.Length)
				{
					int num8 = comp[num++] & 0xFF;
					num5 += num8;
					if (num8 != 255)
					{
						break;
					}
				}
			}
			num5 += 4;
			int num9 = num2 - num7;
			for (int i = 0; i < num5; i++)
			{
				if (num2 >= maxOutput)
				{
					return (outLen: num2, consumedIn: num);
				}
				target[num2++] = target[num9 + i];
			}
		}
		return (outLen: num2, consumedIn: num);
	}
}
