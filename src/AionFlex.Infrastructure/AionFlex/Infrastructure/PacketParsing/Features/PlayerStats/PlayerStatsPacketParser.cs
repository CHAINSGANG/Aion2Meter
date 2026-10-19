using System;
using System.Collections.Generic;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.PlayerStats;

public sealed class PlayerStatsPacketParser : IPlayerStatsPacketParser, IFeaturePacketParser
{
	private readonly ILogger<PlayerStatsPacketParser> _logger;

	private readonly IDataRepository _dataRepository;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private const int MaxValidStatId = 1000;

	private const long MinValidStatValue = -100000L;

	private const long MaxValidStatValue = 100000000L;

	private readonly Dictionary<string, byte[]> _ffffCarryOver = new Dictionary<string, byte[]>();

	private const int MaxFFFFCarryOverBytes = 1048576;

	private readonly LogRateGate _overLimitLog = new LogRateGate();

	private byte[] _decodeScratch = Array.Empty<byte>();

	public event EventHandler<PlayerStatsSnapshot>? PlayerStatsDiscovered;

	public PlayerStatsPacketParser(ILogger<PlayerStatsPacketParser> logger, IDataRepository dataRepository, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger;
		_dataRepository = dataRepository;
		_protocolHealth = protocolHealth;
	}

	public void ProcessPayload(CapturedPayload payload)
	{
		try
		{
			byte[] data = payload.Data;
			if (data == null || data.Length < 8)
			{
				return;
			}
			ScanForEmbeddedPlayerStats(data);
			ScanForEmbeddedFullStats(data);
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
					_logger.LogWarning("\ud83d\udcca PlayerStatsPacketParser FF FF carry-over OVER-LIMIT: stream={Stream} {Bytes}b > {Cap}b — bundle lost{Repeats}", text, num2, 1048576, _overLimitLog.SummarySuffix(repeats));
				}
			}
			else
			{
				_ffffCarryOver.Remove(text);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "PlayerStatsPacketParser: error processing payload");
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
			ScanForEmbeddedPlayerStats(subArray);
			ScanForEmbeddedFullStats(subArray);
		}
		return result;
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

	private void ScanForEmbeddedPlayerStats(byte[] data)
	{
		int num = data.Length;
		for (int i = 0; i < num - 7; i++)
		{
			if ((data[i] != 73 && data[i] != 74) || data[i + 1] != 54)
			{
				continue;
			}
			var (num2, num3) = ReadVarInt(data, i + 2);
			if (num3 < 1 || num3 > 3 || num2 <= 0 || num2 > 999999)
			{
				continue;
			}
			int num4 = i + 2 + num3;
			if (num4 >= num)
			{
				continue;
			}
			byte b = data[num4];
			if (b < 1 || b > 100)
			{
				continue;
			}
			int num5 = num4 + 1;
			if (num5 + b * 6 > num)
			{
				continue;
			}
			int num6 = data[num5] | (data[num5 + 1] << 8);
			long num7 = data[num5 + 2] | (data[num5 + 3] << 8) | (data[num5 + 4] << 16) | (data[num5 + 5] << 24);
			if (num6 > 1000 || num7 < -100000 || num7 > 100000000)
			{
				continue;
			}
			Dictionary<int, long> dictionary = new Dictionary<int, long>(b);
			bool flag = true;
			for (int j = 0; j < b; j++)
			{
				int num8 = num5 + j * 6;
				int num9 = data[num8] | (data[num8 + 1] << 8);
				long num10 = data[num8 + 2] | (data[num8 + 3] << 8) | (data[num8 + 4] << 16) | (data[num8 + 5] << 24);
				if (num9 > 1000 || num10 < -100000 || num10 > 100000000)
				{
					flag = false;
					break;
				}
				dictionary[num9] = num10;
			}
			if (!flag)
			{
				int length = Math.Min(b * 6, num - num5);
				_protocolHealth?.RecordDecodeMiss("stats", data.AsSpan(num5, length));
				continue;
			}
			_logger.LogTrace("\ud83d\udcca {Opcode} 36 OK (PlayerStatsParser): entity={Entity} count={Count}", (data[i] == 73) ? "49" : "4A", num2, b);
			_protocolHealth?.Record((ushort)((data[i] << 8) | 0x36));
			_protocolHealth?.RecordDecodeOk("stats");
			PlayerStatsDiscovered?.Invoke(this, new PlayerStatsSnapshot(num2, dictionary, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
			i = num5 + b * 6 - 1;
		}
	}

	private void ScanForEmbeddedFullStats(byte[] data)
	{
		if (data.Length < 50)
		{
			return;
		}
		int num = data.Length;
		for (int i = 0; i < num - 14; i++)
		{
			if ((data[i] != 72 && data[i] != 73) || data[i + 1] != 54)
			{
				continue;
			}
			int num2 = i + 2;
			if (num2 + 3 > num)
			{
				continue;
			}
			var (num3, num4) = ReadVarInt(data, num2 + 2);
			if (num4 <= 0 || num3 <= 0)
			{
				continue;
			}
			int num5 = num2 + 2 + num4;
			if (num5 + 6 > num)
			{
				continue;
			}
			Dictionary<int, long> dictionary = new Dictionary<int, long>(140);
			int j;
			for (j = num5; j + 6 <= num; j += 6)
			{
				int num6 = data[j] | (data[j + 1] << 8);
				long num7 = data[j + 2] | (data[j + 3] << 8) | (data[j + 4] << 16) | (data[j + 5] << 24);
				if (num6 < 1 || num6 > 1000 || num7 < -100000 || num7 > 100000000)
				{
					break;
				}
				dictionary[num6] = num7;
			}
			if (dictionary.Count >= 20)
			{
				int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
				int num8 = ((localPlayerEntityId.HasValue && localPlayerEntityId.GetValueOrDefault() > 0) ? localPlayerEntityId.Value : 0);
				_logger.LogInformation("\ud83d\udcca {Opcode} 36 OK (PlayerStatsParser full sheet): entity={Entity} count={Count}", (data[i] == 72) ? "48" : "49", num8, dictionary.Count);
				_protocolHealth?.Record((ushort)((data[i] << 8) | 0x36));
				_protocolHealth?.RecordDecodeOk("stats");
				PlayerStatsDiscovered?.Invoke(this, new PlayerStatsSnapshot(num8, dictionary, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
				i = j - 1;
			}
		}
	}

	private static (int Value, int Length) ReadVarInt(byte[] bytes, int offset)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		do
		{
			if (offset + num3 >= bytes.Length)
			{
				return (Value: -1, Length: -1);
			}
			int num4 = bytes[offset + num3] & 0xFF;
			num3++;
			num |= (num4 & 0x7F) << num2;
			if ((num4 & 0x80) == 0)
			{
				return (Value: num, Length: num3);
			}
			num2 += 7;
		}
		while (num2 < 32);
		return (Value: -1, Length: -1);
	}
}
