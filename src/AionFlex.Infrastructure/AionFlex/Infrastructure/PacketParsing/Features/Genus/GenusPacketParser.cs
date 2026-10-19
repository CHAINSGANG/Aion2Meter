using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.Genus;

public sealed class GenusPacketParser : IGenusPacketParser, IFeaturePacketParser
{
	private readonly ILogger<GenusPacketParser> _logger;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private readonly Dictionary<string, byte[]> _ffffCarryOver = new Dictionary<string, byte[]>();

	private const int MaxFFFFCarryOverBytes = 1048576;

	private readonly LogRateGate _overLimitLog = new LogRateGate();

	private byte[] _decodeScratch = Array.Empty<byte>();

	private readonly HashSet<long> _emittedIdentityHashes = new HashSet<long>();

	private readonly Queue<long> _emittedIdentityOrder = new Queue<long>();

	private long _lastLoggedGenusShape = -1L;

	private const int MaxIdentityDedupHistory = 8;

	public event EventHandler<GenusInsightSnapshot>? GenusInsightDiscovered;

	public event EventHandler<(uint CharacterId, uint AccountDiscriminator)>? CharacterIdentityDiscovered;

	public GenusPacketParser(ILogger<GenusPacketParser> logger, ProtocolHealthMonitor? protocolHealth = null)
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
					_logger.LogWarning("\ud83e\uddec GenusPacketParser FF FF carry-over OVER-LIMIT: stream={Stream} {Bytes}b > {Cap}b — bundle lost{Repeats}", text, num2, 1048576, _overLimitLog.SummarySuffix(repeats));
				}
			}
			else
			{
				_ffffCarryOver.Remove(text);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "GenusPacketParser: error processing payload");
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
			ScanForCharacterIdentity(subArray);
			ScanForGenusInsight(subArray);
		}
		return result;
	}

	internal void ScanForCharacterIdentity(ReadOnlySpan<byte> bundle)
	{
		if (bundle.Length < 16 || bundle[0] != 12 || bundle[1] != 70 || bundle[2] != 55 || (bundle[10] == 51 && bundle[11] == 54))
		{
			return;
		}
		uint num = BinaryPrimitives.ReadUInt32LittleEndian(bundle.Slice(8, 4));
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(bundle.Slice(12, 4));
		if (num == 0)
		{
			return;
		}
		long item = (long)(((ulong)num << 32) | num2);
		if (!_emittedIdentityHashes.Contains(item))
		{
			_emittedIdentityHashes.Add(item);
			_emittedIdentityOrder.Enqueue(item);
			while (_emittedIdentityOrder.Count > 8)
			{
				_emittedIdentityHashes.Remove(_emittedIdentityOrder.Dequeue());
			}
			_logger.LogInformation("\ud83c\udd94 Stable character identity decoded (GenusPacketParser): characterId={CharacterId} accountDisc=0x{AcctDisc:X8}", num, num2);
			CharacterIdentityDiscovered?.Invoke(this, (num, num2));
		}
	}

	private void ScanForGenusInsight(byte[] data)
	{
		GenusInsightSnapshot genusInsightSnapshot;
		IReadOnlyList<int> anchoredButFailedOffsets;
		try
		{
			genusInsightSnapshot = GenusInsightParser.Parse(data, out anchoredButFailedOffsets);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "GenusInsightParser threw on a 0C 46 37 bundle ({Bytes} bytes) — skipping", data.Length);
			return;
		}
		for (int i = 0; i < anchoredButFailedOffsets.Count; i++)
		{
			_protocolHealth?.RecordDecodeMiss("genus", data.AsSpan(anchoredButFailedOffsets[i]));
		}
		if (genusInsightSnapshot.Genera.Count != 0)
		{
			_protocolHealth?.RecordDecodeOk("genus");
			int num = 0;
			for (int j = 0; j < genusInsightSnapshot.Genera.Count; j++)
			{
				num += genusInsightSnapshot.Genera[j].Effects.Count;
			}
			long num2 = ((long)genusInsightSnapshot.Genera.Count << 32) | (uint)num;
			if (num2 != _lastLoggedGenusShape)
			{
				_lastLoggedGenusShape = num2;
				_logger.LogInformation("\ud83e\uddec Genus Insight decoded (GenusPacketParser): {Genera} genera, {Effects} active effects", genusInsightSnapshot.Genera.Count, num);
			}
			GenusInsightDiscovered?.Invoke(this, genusInsightSnapshot);
		}
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
