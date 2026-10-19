using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.Wing;

public sealed class WingPacketParser : IWingPacketParser, IFeaturePacketParser
{
	private readonly ILogger<WingPacketParser> _logger;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private IReadOnlySet<int>? _validWingIds;

	private readonly Dictionary<string, byte[]> _ffffCarryOver = new Dictionary<string, byte[]>();

	private const int MaxFFFFCarryOverBytes = 1048576;

	private readonly LogRateGate _overLimitLog = new LogRateGate();

	private byte[] _decodeScratch = Array.Empty<byte>();

	private int _lastActiveLevelsEquipped;

	private readonly HashSet<long> _emittedHashes = new HashSet<long>();

	private readonly Queue<long> _emittedOrder = new Queue<long>();

	private const int MaxDedupHistory = 32;

	public event EventHandler<WingSnapshot>? WingRosterDiscovered;

	public event EventHandler<(string CharacterName, int WingId)>? EquippedWingForCharacterDiscovered;

	public event EventHandler<int>? EquippedWingForLocalPlayerDiscovered;

	private bool AlreadyEmitted(long hash)
	{
		if (_emittedHashes.Contains(hash))
		{
			return true;
		}
		_emittedHashes.Add(hash);
		_emittedOrder.Enqueue(hash);
		while (_emittedOrder.Count > 32)
		{
			_emittedHashes.Remove(_emittedOrder.Dequeue());
		}
		return false;
	}

	public WingPacketParser(ILogger<WingPacketParser> logger, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger;
		_protocolHealth = protocolHealth;
	}

	public void SetValidWingIds(IReadOnlySet<int> wingIds)
	{
		_validWingIds = wingIds;
		_logger.LogInformation("WingPacketParser: loaded {Count} valid wing IDs", wingIds.Count);
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
					_logger.LogWarning("\ud83e\udebd WingPacketParser FF FF carry-over OVER-LIMIT: stream={Stream} {Bytes}b > {Cap}b — bundle lost{Repeats}", text, num2, 1048576, _overLimitLog.SummarySuffix(repeats));
				}
			}
			else
			{
				_ffffCarryOver.Remove(text);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "WingPacketParser: error processing payload");
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
			ScanForWingRoster(subArray);
			ScanCharacterListBundleForEquippedWings(subArray);
			ScanForActiveLevelsEquippedWing(subArray);
		}
		return result;
	}

	private void ScanForWingRoster(byte[] data)
	{
		WingSnapshot wingSnapshot;
		int longestRun;
		int longestRunStart;
		try
		{
			wingSnapshot = WingRosterParser.Parse(data, _validWingIds, out longestRun, out longestRunStart);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "WingRosterParser threw on a bundle ({Bytes} bytes) — skipping", data.Length);
			return;
		}
		if (wingSnapshot.OwnedWings.Count == 0)
		{
			_logger.LogTrace("\ud83e\udebd ScanForWingRoster (WingPacketParser): bundle={Bytes}b wings=0", data.Length);
			if (_validWingIds != null && longestRun >= 1 && longestRunStart >= 0)
			{
				_protocolHealth?.RecordDecodeMiss("wings", data.AsSpan(longestRunStart));
			}
			return;
		}
		_protocolHealth?.RecordDecodeOk("wings");
		long num = -3750763034362895579L;
		foreach (WingEntry ownedWing in wingSnapshot.OwnedWings)
		{
			num = (num ^ ownedWing.WingId) * 1099511628211L;
		}
		if (!AlreadyEmitted(num))
		{
			_logger.LogInformation("\ud83e\udebd ScanForWingRoster (WingPacketParser): bundle={Bytes}b wings={Count}", data.Length, wingSnapshot.OwnedWings.Count);
			WingRosterDiscovered?.Invoke(this, wingSnapshot);
		}
	}

	private void ScanCharacterListBundleForEquippedWings(byte[] data)
	{
		if (data.Length < 1024)
		{
			return;
		}
		IReadOnlySet<int> validWingIds = _validWingIds;
		if (validWingIds == null)
		{
			return;
		}
		List<(int, string)> list = new List<(int, string)>();
		for (int i = 0; i + 1 < data.Length; i++)
		{
			int num = data[i];
			if (num < 4 || num > 15)
			{
				continue;
			}
			int num2 = i + 1;
			int num3 = num2 + num;
			if (num3 + 7 > data.Length)
			{
				continue;
			}
			bool flag = true;
			for (int j = 0; j < num; j++)
			{
				byte b = data[num2 + j];
				bool num4 = (b >= 97 && b <= 122) || (b >= 65 && b <= 90);
				bool flag2 = b >= 48 && b <= 57;
				if (!num4 && !flag2 && b != 95)
				{
					flag = false;
					break;
				}
			}
			if (flag && data[num3 + 4] == 1 && data[num3 + 5] == 2 && data[num3 + 6] == 45)
			{
				string item = Encoding.UTF8.GetString(data, num2, num);
				list.Add((i, item));
				i += num;
			}
		}
		if (list.Count < 2)
		{
			return;
		}
		int num5 = 0;
		int num6 = 0;
		for (int k = 0; k < list.Count; k++)
		{
			int item2 = list[k].Item1;
			byte b2 = data[item2];
			int num7 = item2 + 1 + b2;
			int num8 = ((k + 1 < list.Count) ? list[k + 1].Item1 : data.Length);
			for (int l = num7; l + 8 <= num8; l++)
			{
				int num9 = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(l, 4));
				if (!validWingIds.Contains(num9))
				{
					continue;
				}
				int num10 = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(l + 4, 4));
				if (num10 == 0 || validWingIds.Contains(num10))
				{
					long hash = ((long)list[k].Item2.GetHashCode() << 20) ^ (uint)num9;
					if (AlreadyEmitted(hash))
					{
						num5++;
						break;
					}
					_logger.LogInformation("\ud83e\udebd Equipped wing (WingPacketParser) for '{Name}': wingId={WingId} (secondary={Secondary})", list[k].Item2, num9, num10);
					EquippedWingForCharacterDiscovered?.Invoke(this, (list[k].Item2, num9));
					num5++;
					num6++;
					break;
				}
			}
		}
		if (num6 > 0)
		{
			_logger.LogInformation("\ud83e\udebd Char-list bundle (WingPacketParser): {NameCount} names found, {WingCount} wings emitted ({New} new)", list.Count, num5, num6);
		}
	}

	private void ScanForActiveLevelsEquippedWing(byte[] data)
	{
		if (_validWingIds == null)
		{
			return;
		}
		int? num = WingActiveLevelsParser.FindEquippedWingId(data, _validWingIds);
		if (!num.HasValue)
		{
			return;
		}
		int valueOrDefault = num.GetValueOrDefault();
		if (valueOrDefault != 0)
		{
			_protocolHealth?.RecordDecodeOk("wing_equipped");
			if (valueOrDefault != _lastActiveLevelsEquipped)
			{
				_lastActiveLevelsEquipped = valueOrDefault;
				_logger.LogInformation("\ud83e\udebd Equipped wing (active-levels broadcast, WingPacketParser): wingId={WingId}", valueOrDefault);
				EquippedWingForLocalPlayerDiscovered?.Invoke(this, valueOrDefault);
			}
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
