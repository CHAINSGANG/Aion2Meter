using System;
using System.Collections.Generic;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.Titles;

public sealed class TitlesPacketParser : ITitlesPacketParser, IFeaturePacketParser
{
	private readonly ILogger<TitlesPacketParser> _logger;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private IReadOnlySet<int>? _validTitleIds;

	private readonly Dictionary<string, byte[]> _ffffCarryOver = new Dictionary<string, byte[]>();

	private const int MaxFFFFCarryOverBytes = 1048576;

	private readonly LogRateGate _overLimitLog = new LogRateGate();

	private byte[] _decodeScratch = Array.Empty<byte>();

	private readonly HashSet<long> _emittedHashes = new HashSet<long>();

	private readonly Queue<long> _emittedOrder = new Queue<long>();

	private const int MaxDedupHistory = 32;

	public event EventHandler<EquippedTitlesSnapshot>? EquippedTitlesDiscovered;

	public TitlesPacketParser(ILogger<TitlesPacketParser> logger, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger;
		_protocolHealth = protocolHealth;
	}

	public void SetValidTitleIds(IReadOnlySet<int> titleIds)
	{
		_validTitleIds = titleIds;
		_logger.LogInformation("TitlesPacketParser: loaded {Count} valid title IDs", titleIds.Count);
	}

	public void ProcessPayload(CapturedPayload payload)
	{
		if (_validTitleIds == null)
		{
			return;
		}
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
					_logger.LogWarning("\ud83c\udfc6 TitlesPacketParser FF FF carry-over OVER-LIMIT: stream={Stream} {Bytes}b > {Cap}b — bundle lost{Repeats}", text, num2, 1048576, _overLimitLog.SummarySuffix(repeats));
				}
			}
			else
			{
				_ffffCarryOver.Remove(text);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "TitlesPacketParser: error processing payload");
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
			ScanForEquippedTitles(subArray);
		}
		return result;
	}

	private void ScanForEquippedTitles(byte[] data)
	{
		if (_validTitleIds == null)
		{
			return;
		}
		int num = data.Length;
		List<(int, int, int, int)> list = new List<(int, int, int, int)>();
		for (int i = 0; i < num - 15; i++)
		{
			if (data[i] == 3 && data[i + 1] == 1 && data[i + 6] == 2 && data[i + 11] == 3)
			{
				int num2 = data[i + 2] | (data[i + 3] << 8) | (data[i + 4] << 16) | (data[i + 5] << 24);
				int num3 = data[i + 7] | (data[i + 8] << 8) | (data[i + 9] << 16) | (data[i + 10] << 24);
				int num4 = data[i + 12] | (data[i + 13] << 8) | (data[i + 14] << 16) | (data[i + 15] << 24);
				if (num2 != 0 || num3 != 0 || num4 != 0)
				{
					list.Add((i, num2, num3, num4));
					i += 15;
				}
			}
		}
		List<(int, int, int, int)> list2 = new List<(int, int, int, int)>();
		(int, int, int, int)? tuple = null;
		foreach (var item5 in list)
		{
			if (Whitelisted(item5.Item2) && Whitelisted(item5.Item3) && Whitelisted(item5.Item4))
			{
				list2.Add(item5);
			}
			if (!tuple.HasValue && (RealTitle(item5.Item2) || RealTitle(item5.Item3) || RealTitle(item5.Item4)))
			{
				tuple = item5;
			}
		}
		if (list2.Count != 1)
		{
			if (list.Count > 0)
			{
				_logger.LogTrace("\ud83c\udfc6 ScanForEquippedTitles (TitlesPacketParser): bundle={Bytes}b structural={Cand} whitelisted={Named} — skipped", data.Length, list.Count, list2.Count);
			}
			if (list2.Count == 0 && tuple.HasValue)
			{
				(int, int, int, int) valueOrDefault = tuple.GetValueOrDefault();
				_protocolHealth?.RecordDecodeMiss("titles", data.AsSpan(valueOrDefault.Item1, 16));
			}
			return;
		}
		_protocolHealth?.RecordDecodeOk("titles");
		(int, int, int, int) tuple2 = list2[0];
		int item = tuple2.Item2;
		int item2 = tuple2.Item3;
		int item3 = tuple2.Item4;
		long item4 = ((long)item * 31L + item2) * 31 + item3;
		if (!_emittedHashes.Contains(item4))
		{
			_emittedHashes.Add(item4);
			_emittedOrder.Enqueue(item4);
			while (_emittedOrder.Count > 32)
			{
				_emittedHashes.Remove(_emittedOrder.Dequeue());
			}
			_logger.LogInformation("\ud83c\udfc6 Equipped titles decoded (TitlesPacketParser): slot1={T1} slot2={T2} slot3={T3}", item, item2, item3);
			EquippedTitlesDiscovered?.Invoke(this, new EquippedTitlesSnapshot(item, item2, item3));
		}
		bool RealTitle(int t)
		{
			if (t != 0)
			{
				return _validTitleIds.Contains(t);
			}
			return false;
		}
		bool Whitelisted(int t)
		{
			if (t != 0)
			{
				return _validTitleIds.Contains(t);
			}
			return true;
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
