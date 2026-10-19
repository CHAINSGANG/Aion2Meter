using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using K4os.Compression.LZ4;

namespace AionFlex.Infrastructure.PacketParsing.Dps;

internal sealed class FramedBundleAssembler
{
	public readonly record struct StreamKey(string? Device, int Src, int Dst);

	private readonly record struct Pending(long VarIntStart, long FfOffset, long End, int Dsz, int CompLen);

	private sealed class StreamState
	{
		public byte[] Buf = new byte[4096];

		public int Len;

		public long Base;

		public long Total;

		public long ScanFrom;

		public readonly List<Pending> Pending = new List<Pending>();

		public long LastTouch;
	}

	internal const int MaxDecompressedBytes = 1000000;

	internal const int MaxCompressedBytes = 65536;

	internal const int HeaderOverlapBytes = 8;

	internal const int MaxTrackedStreams = 64;

	internal const int MaxPendingPerStream = 64;

	private const int InitialBufferBytes = 4096;

	private readonly Dictionary<StreamKey, StreamState> _streams = new Dictionary<StreamKey, StreamState>();

	private byte[] _scratch = Array.Empty<byte>();

	private long _seq;

	private static ReadOnlySpan<byte> FfPair => new byte[2] { 255, 255 };

	public long Candidates { get; private set; }

	public long RatioRejected { get; private set; }

	public long OverCap { get; private set; }

	public long DecodeAttempts { get; private set; }

	public long Decoded { get; private set; }

	public long DecodeFailed { get; private set; }

	public long PlayerInfoRecords { get; private set; }

	public long SpawnRecords { get; private set; }

	public long LocalRecords { get; private set; }

	public long RoomRosterRecords { get; private set; }

	public long NestedSkipped { get; private set; }

	public long StreamsEvicted { get; private set; }

	public long EvictedWithPending { get; private set; }

	public long Errors { get; private set; }

	public int MaxRetainedBytes { get; private set; }

	public long PendingRefused { get; private set; }

	public int MaxPendingSeen { get; private set; }

	public void Append(StreamKey key, ReadOnlySpan<byte> payload, ref List<byte[]>? playerInfoRecords, ref List<byte[]>? spawnRecords, ref List<byte[]>? localRecords, ref List<byte[]>? roomRosterRecords)
	{
		List<byte[]> ticketListRecords = null;
		Append(key, payload, ref playerInfoRecords, ref spawnRecords, ref localRecords, ref roomRosterRecords, ref ticketListRecords);
	}

	public void Append(StreamKey key, ReadOnlySpan<byte> payload, ref List<byte[]>? playerInfoRecords, ref List<byte[]>? spawnRecords, ref List<byte[]>? localRecords, ref List<byte[]>? roomRosterRecords, ref List<byte[]>? ticketListRecords)
	{
		List<byte[]> fieldBossListRecords = null;
		Append(key, payload, ref playerInfoRecords, ref spawnRecords, ref localRecords, ref roomRosterRecords, ref ticketListRecords, ref fieldBossListRecords);
	}

	public void Append(StreamKey key, ReadOnlySpan<byte> payload, ref List<byte[]>? playerInfoRecords, ref List<byte[]>? spawnRecords, ref List<byte[]>? localRecords, ref List<byte[]>? roomRosterRecords, ref List<byte[]>? ticketListRecords, ref List<byte[]>? fieldBossListRecords)
	{
		List<byte[]> characterRosterRecords = null;
		Append(key, payload, ref playerInfoRecords, ref spawnRecords, ref localRecords, ref roomRosterRecords, ref ticketListRecords, ref fieldBossListRecords, ref characterRosterRecords);
	}

	public void Append(StreamKey key, ReadOnlySpan<byte> payload, ref List<byte[]>? playerInfoRecords, ref List<byte[]>? spawnRecords, ref List<byte[]>? localRecords, ref List<byte[]>? roomRosterRecords, ref List<byte[]>? ticketListRecords, ref List<byte[]>? fieldBossListRecords, ref List<byte[]>? characterRosterRecords)
	{
		List<byte[]> inventoryRecords = null;
		Append(key, payload, ref playerInfoRecords, ref spawnRecords, ref localRecords, ref roomRosterRecords, ref ticketListRecords, ref fieldBossListRecords, ref characterRosterRecords, ref inventoryRecords);
	}

	public void Append(StreamKey key, ReadOnlySpan<byte> payload, ref List<byte[]>? playerInfoRecords, ref List<byte[]>? spawnRecords, ref List<byte[]>? localRecords, ref List<byte[]>? roomRosterRecords, ref List<byte[]>? ticketListRecords, ref List<byte[]>? fieldBossListRecords, ref List<byte[]>? characterRosterRecords, ref List<byte[]>? inventoryRecords)
	{
		if (payload.IsEmpty)
		{
			return;
		}
		try
		{
			StreamState orCreate = GetOrCreate(key);
			orCreate.LastTouch = ++_seq;
			EnsureCapacity(orCreate, orCreate.Len + payload.Length);
			payload.CopyTo(orCreate.Buf.AsSpan(orCreate.Len));
			orCreate.Len += payload.Length;
			orCreate.Total += payload.Length;
			Scan(orCreate);
			Resolve(orCreate, ref playerInfoRecords, ref spawnRecords, ref localRecords, ref roomRosterRecords, ref ticketListRecords, ref fieldBossListRecords, ref characterRosterRecords, ref inventoryRecords);
			Trim(orCreate);
			if (orCreate.Len > MaxRetainedBytes)
			{
				MaxRetainedBytes = orCreate.Len;
			}
		}
		catch
		{
			_streams.Remove(key);
			Errors++;
		}
	}

	private StreamState GetOrCreate(StreamKey key)
	{
		if (_streams.TryGetValue(key, out StreamState value))
		{
			return value;
		}
		StreamState streamState = null;
		if (_streams.Count >= 64)
		{
			StreamKey key2 = default;
			long num = long.MaxValue;
			foreach (var (streamKey2, streamState3) in _streams)
			{
				if (streamState3.LastTouch < num)
				{
					num = streamState3.LastTouch;
					key2 = streamKey2;
				}
			}
			if (_streams.Remove(key2, out StreamState value2))
			{
				StreamsEvicted++;
				if (value2.Pending.Count > 0)
				{
					EvictedWithPending++;
				}
				streamState = value2;
			}
		}
		StreamState streamState4 = streamState ?? new StreamState();
		streamState4.Len = 0;
		streamState4.Base = 0L;
		streamState4.Total = 0L;
		streamState4.ScanFrom = 0L;
		streamState4.Pending.Clear();
		_streams[key] = streamState4;
		return streamState4;
	}

	private static void EnsureCapacity(StreamState st, int needed)
	{
		if (needed > st.Buf.Length)
		{
			byte[] array = new byte[Math.Max(needed, st.Buf.Length * 2)];
			Buffer.BlockCopy(st.Buf, 0, array, 0, st.Len);
			st.Buf = array;
		}
	}

	private void Scan(StreamState st)
	{
		long num = Math.Max(st.ScanFrom, st.Base);
		long num4;
		while (true)
		{
			int num2 = (int)(num - st.Base);
			int num3 = ((num2 < st.Len - 1) ? st.Buf.AsSpan(num2, st.Len - num2).IndexOf(FfPair) : (-1));
			if (num3 < 0)
			{
				st.ScanFrom = Math.Max(st.Base, st.Total - 1);
				return;
			}
			num4 = num + num3;
			if (num4 + 6 > st.Total)
			{
				break;
			}
			AddCandidates(st, num4);
			num = num4 + 1;
		}
		st.ScanFrom = num4;
	}

	private void AddCandidates(StreamState st, long a)
	{
		int num = (int)(a - st.Base);
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(st.Buf.AsSpan(num + 2, 4));
		if (num2 < 1 || num2 > 1000000)
		{
			return;
		}
		for (int num3 = 3; num3 >= 1; num3--)
		{
			long num4 = a - num3;
			if (num4 >= st.Base)
			{
				(int Value, int Length) tuple = ReadVarInt(st.Buf.AsSpan(0, st.Len), (int)(num4 - st.Base));
				var (num5, _) = tuple;
				if (tuple.Length == num3)
				{
					long num6 = (long)num5 - 10L;
					if (num6 > 65536)
					{
						OverCap++;
					}
					else if (num6 < 1 || num2 > num6 * 255 || num6 > num2 + num2 / 255 + 16)
					{
						RatioRejected++;
					}
					else if (st.Pending.Count >= 64)
					{
						PendingRefused++;
					}
					else
					{
						st.Pending.Add(new Pending(num4, a, a + 6 + num6, (int)num2, (int)num6));
						if (st.Pending.Count > MaxPendingSeen)
						{
							MaxPendingSeen = st.Pending.Count;
						}
						Candidates++;
					}
				}
			}
		}
	}

	private void Resolve(StreamState st, ref List<byte[]>? playerInfoOutput, ref List<byte[]>? spawnOutput, ref List<byte[]>? localOutput, ref List<byte[]>? roomRosterOutput, ref List<byte[]>? ticketListOutput, ref List<byte[]>? fieldBossListOutput, ref List<byte[]>? characterRosterOutput, ref List<byte[]>? inventoryOutput)
	{
		int num = 0;
		while (num < st.Pending.Count)
		{
			Pending p = st.Pending[num];
			if (p.End > st.Total)
			{
				num++;
				continue;
			}
			st.Pending.RemoveAt(num);
			DecodeAttempts++;
			if (_scratch.Length < p.Dsz)
			{
				_scratch = new byte[Math.Min(1000000, Math.Max(p.Dsz, _scratch.Length * 2))];
			}
			int num2;
			try
			{
				num2 = LZ4Codec.Decode(st.Buf.AsSpan((int)(p.FfOffset + 6 - st.Base), p.CompLen), _scratch.AsSpan(0, p.Dsz));
			}
			catch
			{
				num2 = -1;
			}
			if (num2 != p.Dsz)
			{
				DecodeFailed++;
				continue;
			}
			Decoded++;
			var (num3, num4, num5, num6, num7) = CollectLaneRecords(_scratch.AsSpan(0, p.Dsz), ref playerInfoOutput, ref spawnOutput, ref localOutput, ref roomRosterOutput, ref ticketListOutput, ref fieldBossListOutput, ref characterRosterOutput, ref inventoryOutput);
			PlayerInfoRecords += num3;
			SpawnRecords += num4;
			LocalRecords += num5;
			RoomRosterRecords += num6;
			NestedSkipped += num7;
			st.Pending.RemoveAll((Pending x) => x.FfOffset == p.FfOffset);
			num = 0;
		}
	}

	private static void Trim(StreamState st)
	{
		long num = Math.Min(st.Total - 8, st.ScanFrom);
		foreach (Pending item in st.Pending)
		{
			if (item.VarIntStart < num)
			{
				num = item.VarIntStart;
			}
		}
		if (num > st.Base)
		{
			int num2 = (int)(num - st.Base);
			Buffer.BlockCopy(st.Buf, num2, st.Buf, 0, st.Len - num2);
			st.Len -= num2;
			st.Base = num;
		}
	}

	internal static (int PlayerInfo, int Spawns, int Local, int RoomRosters, int Nested) CollectLaneRecords(ReadOnlySpan<byte> dec, ref List<byte[]>? playerInfoOutput, ref List<byte[]>? spawnOutput, ref List<byte[]>? localOutput, ref List<byte[]>? roomRosterOutput)
	{
		List<byte[]> ticketListOutput = null;
		return CollectLaneRecords(dec, ref playerInfoOutput, ref spawnOutput, ref localOutput, ref roomRosterOutput, ref ticketListOutput);
	}

	internal static (int PlayerInfo, int Spawns, int Local, int RoomRosters, int Nested) CollectLaneRecords(ReadOnlySpan<byte> dec, ref List<byte[]>? playerInfoOutput, ref List<byte[]>? spawnOutput, ref List<byte[]>? localOutput, ref List<byte[]>? roomRosterOutput, ref List<byte[]>? ticketListOutput)
	{
		List<byte[]> fieldBossListOutput = null;
		return CollectLaneRecords(dec, ref playerInfoOutput, ref spawnOutput, ref localOutput, ref roomRosterOutput, ref ticketListOutput, ref fieldBossListOutput);
	}

	internal static (int PlayerInfo, int Spawns, int Local, int RoomRosters, int Nested) CollectLaneRecords(ReadOnlySpan<byte> dec, ref List<byte[]>? playerInfoOutput, ref List<byte[]>? spawnOutput, ref List<byte[]>? localOutput, ref List<byte[]>? roomRosterOutput, ref List<byte[]>? ticketListOutput, ref List<byte[]>? fieldBossListOutput)
	{
		List<byte[]> characterRosterOutput = null;
		return CollectLaneRecords(dec, ref playerInfoOutput, ref spawnOutput, ref localOutput, ref roomRosterOutput, ref ticketListOutput, ref fieldBossListOutput, ref characterRosterOutput);
	}

	internal static (int PlayerInfo, int Spawns, int Local, int RoomRosters, int Nested) CollectLaneRecords(ReadOnlySpan<byte> dec, ref List<byte[]>? playerInfoOutput, ref List<byte[]>? spawnOutput, ref List<byte[]>? localOutput, ref List<byte[]>? roomRosterOutput, ref List<byte[]>? ticketListOutput, ref List<byte[]>? fieldBossListOutput, ref List<byte[]>? characterRosterOutput)
	{
		List<byte[]> inventoryOutput = null;
		return CollectLaneRecords(dec, ref playerInfoOutput, ref spawnOutput, ref localOutput, ref roomRosterOutput, ref ticketListOutput, ref fieldBossListOutput, ref characterRosterOutput, ref inventoryOutput);
	}

	internal static (int PlayerInfo, int Spawns, int Local, int RoomRosters, int Nested) CollectLaneRecords(ReadOnlySpan<byte> dec, ref List<byte[]>? playerInfoOutput, ref List<byte[]>? spawnOutput, ref List<byte[]>? localOutput, ref List<byte[]>? roomRosterOutput, ref List<byte[]>? ticketListOutput, ref List<byte[]>? fieldBossListOutput, ref List<byte[]>? characterRosterOutput, ref List<byte[]>? inventoryOutput)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int length = dec.Length;
		while (num6 < length)
		{
			if (dec[num6] == 0)
			{
				num6++;
				continue;
			}
			var (num7, num8) = ReadVarInt(dec, num6);
			if (num8 <= 0 || num7 < 6)
			{
				break;
			}
			long num9 = (long)num8 + (long)num7 - 4;
			if (num6 + num9 > length)
			{
				break;
			}
			byte b = dec[num6 + num8];
			byte b2 = dec[num6 + num8 + 1];
			if (b == byte.MaxValue && b2 == byte.MaxValue)
			{
				num5++;
			}
			else if (b2 == 54 && (b == 69 || b == 68))
			{
				List<byte[]>? obj = playerInfoOutput ?? (playerInfoOutput = new List<byte[]>());
				int num10 = num6;
				obj.Add(dec.Slice(num10, (int)Math.Min((long)num6 + (long)num7, length) - num10).ToArray());
				num++;
			}
			else if (b2 == 54 && (b == 65 || b == 64))
			{
				List<byte[]>? obj2 = spawnOutput ?? (spawnOutput = new List<byte[]>());
				int num10 = num6;
				obj2.Add(dec.Slice(num10, (int)Math.Min((long)num6 + (long)num7, length) - num10).ToArray());
				num2++;
			}
			else if (b2 == 54 && b == 51)
			{
				List<byte[]>? obj3 = localOutput ?? (localOutput = new List<byte[]>());
				int num10 = num6;
				obj3.Add(dec.Slice(num10, (int)Math.Min((long)num6 + (long)num7, length) - num10).ToArray());
				num3++;
			}
			else if (b == 2 && b2 == 151)
			{
				List<byte[]>? obj4 = roomRosterOutput ?? (roomRosterOutput = new List<byte[]>());
				int num10 = num6;
				obj4.Add(dec.Slice(num10, (int)(num6 + num9) - num10).ToArray());
				num4++;
			}
			else if (b == 11 && b2 == 97)
			{
				List<byte[]>? obj5 = ticketListOutput ?? (ticketListOutput = new List<byte[]>());
				int num10 = num6;
				obj5.Add(dec.Slice(num10, (int)(num6 + num9) - num10).ToArray());
			}
			else if (b == 1 && b2 == 145)
			{
				List<byte[]>? obj6 = fieldBossListOutput ?? (fieldBossListOutput = new List<byte[]>());
				int num10 = num6;
				obj6.Add(dec.Slice(num10, (int)(num6 + num9) - num10).ToArray());
			}
			else if (b2 == 54 && (b == 38 || b == 21))
			{
				List<byte[]>? obj7 = characterRosterOutput ?? (characterRosterOutput = new List<byte[]>());
				int num10 = num6;
				obj7.Add(dec.Slice(num10, (int)(num6 + num9) - num10).ToArray());
			}
			else if (b2 == 86 && (b == 17 || b == 27))
			{
				List<byte[]>? obj8 = inventoryOutput ?? (inventoryOutput = new List<byte[]>());
				int num10 = num6;
				obj8.Add(dec.Slice(num10, (int)(num6 + num9) - num10).ToArray());
			}
			num6 += (int)num9;
		}
		return (PlayerInfo: num, Spawns: num2, Local: num3, RoomRosters: num4, Nested: num5);
	}

	private static (int Value, int Length) ReadVarInt(ReadOnlySpan<byte> bytes, int offset)
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 5; i++)
		{
			if (offset + i >= bytes.Length)
			{
				return (Value: -1, Length: -1);
			}
			byte b = bytes[offset + i];
			num |= (b & 0x7F) << num2;
			if ((b & 0x80) == 0)
			{
				if (num >= 0)
				{
					return (Value: num, Length: i + 1);
				}
				return (Value: -1, Length: -1);
			}
			num2 += 7;
		}
		return (Value: -1, Length: -1);
	}
}
