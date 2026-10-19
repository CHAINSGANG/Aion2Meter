using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Features.Gear;

public sealed class GearPacketParser : IGearPacketParser, IFeaturePacketParser
{
	private readonly ILogger<GearPacketParser> _logger;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private IReadOnlySet<int>? _validItemIds;

	private readonly Dictionary<string, byte[]> _ffffCarryOver = new Dictionary<string, byte[]>();

	private const int MaxFFFFCarryOverBytes = 1048576;

	private readonly LogRateGate _overLimitLog = new LogRateGate();

	private byte[] _decodeScratch = Array.Empty<byte>();

	private readonly HashSet<long> _emittedHashes = new HashSet<long>();

	private readonly Queue<long> _emittedOrder = new Queue<long>();

	private const int MaxDedupHistory = 64;

	public event EventHandler<IReadOnlyList<GearLoginBundleParser.BundleItem>>? LoginGearBundleDecoded;

	public event EventHandler<(int ItemId, IReadOnlyList<GearSubStat> SubStats)>? GearDetailDecoded;

	public event EventHandler<GearChange>? GearChangeDetected;

	public event EventHandler<(string CharacterName, IReadOnlyList<int> ItemIds)>? EquippedGearForCharacterDiscovered;

	public event EventHandler<IReadOnlyList<int>>? LobbyGearListDiscovered;

	private bool AlreadyEmitted(long hash)
	{
		if (_emittedHashes.Contains(hash))
		{
			return true;
		}
		_emittedHashes.Add(hash);
		_emittedOrder.Enqueue(hash);
		while (_emittedOrder.Count > 64)
		{
			_emittedHashes.Remove(_emittedOrder.Dequeue());
		}
		return false;
	}

	private static long FnvOf(IEnumerable<int> values, long salt)
	{
		long num = -3750763034362895579L ^ salt;
		foreach (int value in values)
		{
			num = (num ^ value) * 1099511628211L;
		}
		return num;
	}

	public GearPacketParser(ILogger<GearPacketParser> logger, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger;
		_protocolHealth = protocolHealth;
	}

	public void SetValidItemIds(IReadOnlySet<int> itemIds)
	{
		_validItemIds = itemIds;
		_logger.LogInformation("GearPacketParser: loaded {Count} valid item IDs", itemIds.Count);
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
			if (array.Length >= 12)
			{
				int num = ScanForUnframedBundles(array);
				if (num >= 0)
				{
					int num2 = array.Length - num;
					if (num2 <= 1048576)
					{
						_ffffCarryOver[text] = array[num..];
					}
					else
					{
						_ffffCarryOver.Remove(text);
						if (_overLimitLog.ShouldLog(text, out var repeats))
						{
							_logger.LogWarning("\ud83d\udee1\ufe0f GearPacketParser FF FF carry-over OVER-LIMIT: stream={Stream} {Bytes}b > {Cap}b — bundle lost{Repeats}", text, num2, 1048576, _overLimitLog.SummarySuffix(repeats));
						}
					}
				}
				else
				{
					_ffffCarryOver.Remove(text);
				}
			}
			ScanForFramedGearPackets(data);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "GearPacketParser: error processing payload");
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
			ScanForLoginGearBundle(subArray);
			ScanForGearInCharacterListBundle(subArray);
		}
		return result;
	}

	private void ScanForLoginGearBundle(byte[] data)
	{
		if (data.Length < 1000)
		{
			return;
		}
		IReadOnlySet<int> validItemIds = _validItemIds;
		if (validItemIds == null)
		{
			return;
		}
		IReadOnlyList<GearLoginBundleParser.BundleItem> readOnlyList = GearLoginBundleParser.Parse(data, validItemIds);
		if (readOnlyList.Count == 0)
		{
			return;
		}
		if (readOnlyList.Count < 5)
		{
			if (_protocolHealth != null)
			{
				byte[] array = new byte[readOnlyList.Count * 4];
				for (int i = 0; i < readOnlyList.Count; i++)
				{
					BinaryPrimitives.WriteInt32LittleEndian(array.AsSpan(i * 4, 4), readOnlyList[i].ItemId);
				}
				_protocolHealth.RecordDecodeMiss("gear", array);
			}
			return;
		}
		_protocolHealth?.RecordDecodeOk("gear");
		if (!AlreadyEmitted(FnvOf(readOnlyList.Select((GearLoginBundleParser.BundleItem it) => it.ItemId), 1L)))
		{
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("\ud83d\udee1\ufe0f Login gear bundle decoded (GearPacketParser): {Count} equipped slots", readOnlyList.Count);
			}
			LoginGearBundleDecoded?.Invoke(this, readOnlyList);
		}
	}

	private void ScanForGearInCharacterListBundle(byte[] data)
	{
		if (data.Length < 1024)
		{
			return;
		}
		IReadOnlySet<int> validItemIds = _validItemIds;
		if (validItemIds == null)
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
		for (int k = 0; k < list.Count; k++)
		{
			int item2 = list[k].Item1;
			byte b2 = data[item2];
			int num6 = item2 + 1 + b2;
			int num7 = ((k + 1 < list.Count) ? list[k + 1].Item1 : data.Length);
			List<int> list2 = new List<int>();
			for (int l = num6; l + 4 <= num7; l++)
			{
				int item3 = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(l, 4));
				if (validItemIds.Contains(item3))
				{
					list2.Add(item3);
					l += 5;
				}
			}
			if (list2.Count > 0)
			{
				long hash = FnvOf(list2, 2L) ^ ((long)list[k].Item2.GetHashCode() << 24);
				if (!AlreadyEmitted(hash))
				{
					_logger.LogInformation("\ud83d\udee1\ufe0f Equipped gear (GearPacketParser) for '{Name}': {Count} items found", list[k].Item2, list2.Count);
					EquippedGearForCharacterDiscovered?.Invoke(this, (list[k].Item2, list2));
					num5++;
				}
			}
		}
		if (num5 > 0)
		{
			_logger.LogInformation("\ud83d\udee1\ufe0f Char-list bundle (GearPacketParser): {NameCount} names, {GearCount} gear lists emitted", list.Count, num5);
		}
	}

	private void ScanForFramedGearPackets(byte[] payload)
	{
		if (payload.Length < 8)
		{
			return;
		}
		for (int i = 1; i < payload.Length - 4; i++)
		{
			byte b = payload[i];
			byte b2 = payload[i + 1];
			bool flag = b == 27 && b2 == 86;
			bool flag2 = b == 28 && b2 == 86;
			bool flag3 = b == 14 && b2 == 141;
			if (!flag && !flag2 && !flag3)
			{
				continue;
			}
			for (int j = 1; j <= 3 && i - j >= 0; j++)
			{
				var (num, num2) = ReadVarInt(payload, i - j);
				if (num2 != j || num < 6 || num > 8000)
				{
					continue;
				}
				int num3 = i - j;
				int num4 = Math.Min(num3 + num + num2, payload.Length);
				if (num4 - num3 >= 6)
				{
					byte[] subArray = payload[num3..num4];
					if (flag)
					{
						TryParse1B56DetailPacket(subArray);
					}
					else if (flag2)
					{
						TryParse1C56Packet(subArray);
					}
					else if (flag3)
					{
						TryParse0E8DPacket(subArray);
					}
					break;
				}
			}
		}
	}

	private bool TryParse1B56DetailPacket(byte[] packet)
	{
		int num = 0;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 27 || packet[num + 1] != 86)
		{
			return false;
		}
		num += 2;
		if (num + 2 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 2 || packet[num + 1] != 1 || packet[num + 2] != 0)
		{
			return false;
		}
		int num4 = num2 - 2;
		if (num4 <= 0)
		{
			return true;
		}
		int num5 = Math.Min(num + num4, packet.Length);
		ReadOnlySpan<byte> body = new ReadOnlySpan<byte>(packet, num, num5 - num);
		IReadOnlySet<int> validItemIds = _validItemIds;
		if (validItemIds == null)
		{
			return true;
		}
		GearDetailParser.DetailRecord detailRecord = GearDetailParser.Parse(body, validItemIds);
		if ((object)detailRecord == null)
		{
			return true;
		}
		if (AlreadyEmitted(FnvOf(new int[2]
		{
			detailRecord.ItemId,
			detailRecord.SubStats.Count
		}, 3L)))
		{
			return true;
		}
		_logger.LogDebug("\ud83d\udee1\ufe0f 1B 56 detail (GearPacketParser): item {Item} {Count} substats", detailRecord.ItemId, detailRecord.SubStats.Count);
		GearDetailDecoded?.Invoke(this, (detailRecord.ItemId, detailRecord.SubStats));
		return true;
	}

	private bool TryParse1C56Packet(byte[] packet)
	{
		int num = 0;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 28 || packet[num + 1] != 86)
		{
			return false;
		}
		num += 2;
		IReadOnlySet<int> validItemIds = _validItemIds;
		if (validItemIds == null)
		{
			return true;
		}
		int num4 = num2 - 2;
		if (num4 <= 0)
		{
			return false;
		}
		int num5 = Math.Min(num + num4, packet.Length);
		if (num5 <= num)
		{
			return false;
		}
		GearChange gearChange = GearChangeParser.Parse(new ReadOnlySpan<byte>(packet, num, num5 - num), validItemIds);
		if ((object)gearChange == null)
		{
			return true;
		}
		long hash = FnvOf(new int[3]
		{
			gearChange.EntityId,
			gearChange.OldItem?.ItemId ?? (-1),
			gearChange.NewItem?.ItemId ?? (-1)
		}, 4L);
		if (AlreadyEmitted(hash))
		{
			return true;
		}
		_logger.LogDebug("\ud83d\udee1\ufe0f Gear change (GearPacketParser) for entity {Entity}", gearChange.EntityId);
		GearChangeDetected?.Invoke(this, gearChange);
		return true;
	}

	private bool TryParse0E8DPacket(byte[] packet)
	{
		int num = 0;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 14 || packet[num + 1] != 141)
		{
			return false;
		}
		num += 2;
		IReadOnlySet<int> validItemIds = _validItemIds;
		if (validItemIds == null)
		{
			return true;
		}
		int num4 = num2 - 2;
		if (num4 <= 0)
		{
			return false;
		}
		int num5 = Math.Min(num + num4, packet.Length);
		if (num5 <= num)
		{
			return false;
		}
		ReadOnlySpan<byte> readOnlySpan = new ReadOnlySpan<byte>(packet, num, num5 - num);
		List<int> list = new List<int>();
		for (int i = 0; i + 4 <= readOnlySpan.Length; i++)
		{
			int item = BinaryPrimitives.ReadInt32LittleEndian(readOnlySpan.Slice(i, 4));
			if (validItemIds.Contains(item))
			{
				list.Add(item);
			}
		}
		if (list.Count == 0)
		{
			return true;
		}
		if (AlreadyEmitted(FnvOf(list, 5L)))
		{
			return true;
		}
		_logger.LogDebug("\ud83d\udee1\ufe0f 0E 8D (GearPacketParser): lobby char list with {Count} items", list.Count);
		LobbyGearListDiscovered?.Invoke(this, list);
		return true;
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
