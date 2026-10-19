using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AionFlex.Infrastructure.PacketParsing.Dps;

internal sealed class TunnelFrameUnwrapper
{
	public readonly record struct StreamKey(string? Device, int Src, int Dst);

	public readonly record struct GameSegment(int Src, int Dst, byte[] Data, bool FirstOfConnection, long ArrivedMs, string? Device);

	public enum Anomaly
	{
		BadFrame,
		MissingIdentity,
		UnrecognisedFrame,
		LostAfterSkip
	}

	private sealed class StreamState
	{
		public byte[] Buf = new byte[4096];

		public int Len;

		public long LastTouch;

		public bool Synced = true;
	}

	private readonly record struct ConnectionKey(ulong IdHead, ulong IdTail, byte Direction);

	private readonly record struct HeldFrame(byte[] Data, long ArrivedMs);

	private sealed class LegState
	{
		public StreamKey Key;

		public ulong MaxSequence;

		public long LastSeenMs;
	}

	private sealed class ConnectionState
	{
		public ulong LastDelivered;

		public bool DeliveredAny;

		public string? Device;

		public int Src;

		public int Dst;

		public long LastTouch;

		public SortedDictionary<ulong, HeldFrame>? Held;

		public Queue<(ulong Sequence, long ArrivedMs)>? HeldOrder;

		public readonly List<LegState> Legs = new List<LegState>(2);

		public List<(ulong First, ulong Last, long AtMs)>? SkippedRanges;
	}

	private readonly record struct Frame(int Kind, int Direction, int ConnectionOffset, int ConnectionLength, int SequenceOffset, int SequenceLength, int GameOffset, int GameLength);

	internal const int FrameHeaderBytes = 4;

	internal const int SignatureBytes = 7;

	internal const int MaxTrackedStreams = 256;

	internal const int MaxTrackedConnections = 256;

	internal const long ReorderHoldMs = 5000L;

	internal const int MaxHeldBytes = 4194304;

	internal const int MaxHeldFrames = 4096;

	internal const int MaxSkippedRangesPerConnection = 16;

	internal const int MaxLegsPerConnection = 8;

	private const int InitialBufferBytes = 4096;

	private const int ConnectionIdBytes = 16;

	private const byte FrameMarker = 1;

	private const byte TagFlag = 1;

	private const byte TagShort = 5;

	private const byte TagLong = 9;

	private const byte TypeKind = 1;

	private const byte TypeDirection = 2;

	private const byte TypeConnection = 3;

	private const byte TypeSequence = 34;

	private const byte TypeGameBytes = 35;

	private const byte KindData = 9;

	private readonly Dictionary<StreamKey, StreamState> _streams = new Dictionary<StreamKey, StreamState>();

	private readonly Dictionary<ConnectionKey, ConnectionState> _connections = new Dictionary<ConnectionKey, ConnectionState>();

	private readonly HashSet<ConnectionState> _holding = new HashSet<ConnectionState>(ReferenceEqualityComparer.Instance);

	private readonly List<ConnectionState> _sweep = new List<ConnectionState>();

	private long _touch;

	private long _heldBytes;

	private int _heldFrames;

	public Action<StreamKey, Anomaly, int>? AnomalyObserved { get; set; }

	public long StreamsDetected { get; private set; }

	public long TunnelPayloads { get; private set; }

	public long DataFrames { get; private set; }

	public long Delivered { get; private set; }

	public long Duplicates { get; private set; }

	public long ControlFrames { get; private set; }

	public long BadFrames { get; private set; }

	public long MissingIdentity { get; private set; }

	public long UnrecognisedFrames { get; private set; }

	public long Desyncs { get; private set; }

	public long Resyncs { get; private set; }

	public long ResyncSkippedBytes { get; private set; }

	public long HeldFrames { get; private set; }

	public long LateFills { get; private set; }

	public long LostAfterSkip { get; private set; }

	public long GapsSkipped { get; private set; }

	public long SkippedSequences { get; private set; }

	public long MaxHoldMs { get; private set; }

	public int MaxHeldBytesSeen { get; private set; }

	public long StreamsEvicted { get; private set; }

	public long ConnectionsSeen { get; private set; }

	public long ConnectionsEvicted { get; private set; }

	public int MaxRetainedBytes { get; private set; }

	public long Errors { get; private set; }

	public long HeldBytesNow => _heldBytes;

	public static bool StartsFrame(ReadOnlySpan<byte> data)
	{
		if (data.Length >= 7 && data[0] == 1 && data[3] == 0 && data[4] == 5 && data[5] == 1)
		{
			return data[6] == 1;
		}
		return false;
	}

	public bool Append(StreamKey key, ReadOnlySpan<byte> payload, long nowMs, ref List<GameSegment>? segments)
	{
		if (_holding.Count > 0)
		{
			SweepHolds(nowMs, ref segments);
		}
		if (payload.IsEmpty)
		{
			return false;
		}
		if (!_streams.TryGetValue(key, out StreamState value))
		{
			if (!StartsFrame(payload) || !FirstFrameWalks(payload))
			{
				return false;
			}
			value = Track(key);
			StreamsDetected++;
		}
		try
		{
			AppendToTunnel(key, value, payload, nowMs, ref segments);
		}
		catch
		{
			value.Buf = new byte[4096];
			value.Len = 0;
			value.Synced = false;
			Errors++;
		}
		return true;
	}

	private void AppendToTunnel(StreamKey key, StreamState st, ReadOnlySpan<byte> payload, long nowMs, ref List<GameSegment>? segments)
	{
		st.LastTouch = ++_touch;
		TunnelPayloads++;
		EnsureCapacity(st, st.Len + payload.Length);
		payload.CopyTo(st.Buf.AsSpan(st.Len));
		st.Len += payload.Length;
		if (st.Len > MaxRetainedBytes)
		{
			MaxRetainedBytes = st.Len;
		}
		int num = 0;
		while (st.Len - num >= 4)
		{
			if (!st.Synced)
			{
				int num2 = FindFrameStart(st.Buf.AsSpan(0, st.Len), num);
				if (num2 < 0)
				{
					int num3 = Math.Max(num, st.Len - 6);
					ResyncSkippedBytes += num3 - num;
					num = num3;
					break;
				}
				ResyncSkippedBytes += num2 - num;
				Resyncs++;
				num = num2;
				st.Synced = true;
			}
			if (st.Buf[num] != 1 || st.Buf[num + 3] != 0)
			{
				Desyncs++;
				st.Synced = false;
				continue;
			}
			int num4 = num + 4 + BinaryPrimitives.ReadUInt16LittleEndian(st.Buf.AsSpan(num + 1, 2));
			if (num4 > st.Len)
			{
				break;
			}
			if (!ReadFrame(key, st.Buf.AsSpan(num + 4, num4 - num - 4), nowMs, ref segments))
			{
				st.Synced = false;
				num++;
			}
			else
			{
				num = num4;
			}
		}
		if (num > 0)
		{
			int num5 = st.Len - num;
			if (num5 > 0)
			{
				Buffer.BlockCopy(st.Buf, num, st.Buf, 0, num5);
			}
			st.Len = num5;
		}
		if (st.Len == 0 && st.Buf.Length > 16384)
		{
			st.Buf = new byte[4096];
		}
	}

	private static int FindFrameStart(ReadOnlySpan<byte> buf, int from)
	{
		ReadOnlySpan<byte> value = new byte[4] { 0, 5, 1, 1 };
		int num = from;
		while (num + 7 <= buf.Length)
		{
			ref ReadOnlySpan<byte> reference = ref buf;
			int num2 = num + 3;
			int num3 = reference.Slice(num2, reference.Length - num2).IndexOf(value);
			if (num3 < 0)
			{
				return -1;
			}
			int num4 = num + num3;
			if (buf[num4] == 1)
			{
				reference = ref buf;
				num2 = num4;
				if (FirstFrameWalks(reference.Slice(num2, reference.Length - num2)))
				{
					return num4;
				}
			}
			num = num4 + 1;
		}
		return -1;
	}

	private static bool FirstFrameWalks(ReadOnlySpan<byte> data)
	{
		int num = 4 + BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(1, 2));
		Frame frame;
		if (num <= data.Length)
		{
			return TryWalk(data.Slice(4, num - 4), out frame);
		}
		return true;
	}

	private bool ReadFrame(StreamKey key, ReadOnlySpan<byte> body, long nowMs, ref List<GameSegment>? segments)
	{
		if (!TryWalk(body, out var frame))
		{
			BadFrames++;
			AnomalyObserved?.Invoke(key, Anomaly.BadFrame, body.Length);
			return false;
		}
		if (frame.Kind != 9)
		{
			ControlFrames++;
			if (frame.GameLength >= 0 || !IsKnownControlKind(frame.Kind))
			{
				UnrecognisedFrames++;
				AnomalyObserved?.Invoke(key, Anomaly.UnrecognisedFrame, frame.Kind);
			}
			return true;
		}
		DataFrames++;
		if (frame.GameLength < 0)
		{
			UnrecognisedFrames++;
			AnomalyObserved?.Invoke(key, Anomaly.UnrecognisedFrame, frame.Kind);
			return true;
		}
		bool flag = frame.Direction < 0 || frame.ConnectionLength != 16;
		if (!flag)
		{
			int sequenceLength = frame.SequenceLength;
			bool flag2 = ((sequenceLength < 1 || sequenceLength > 8) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			MissingIdentity++;
			AnomalyObserved?.Invoke(key, Anomaly.MissingIdentity, frame.Kind);
			return true;
		}
		ulong num = 0uL;
		ReadOnlySpan<byte> readOnlySpan = body.Slice(frame.SequenceOffset, frame.SequenceLength);
		for (int num2 = readOnlySpan.Length - 1; num2 >= 0; num2--)
		{
			num = (num << 8) | readOnlySpan[num2];
		}
		ReadOnlySpan<byte> source = body.Slice(frame.ConnectionOffset, 16);
		ConnectionKey connectionKey = new ConnectionKey(BinaryPrimitives.ReadUInt64LittleEndian(source), BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(8, source.Length - 8)), (byte)frame.Direction);
		bool flag3 = !_connections.TryGetValue(connectionKey, out ConnectionState value);
		if (flag3)
		{
			value = TrackConnection(connectionKey, key, nowMs, ref segments);
		}
		value.LastTouch = ++_touch;
		NoteLeg(value, key, num, nowMs);
		ReadOnlySpan<byte> readOnlySpan2 = body.Slice(frame.GameOffset, frame.GameLength);
		SortedDictionary<ulong, HeldFrame> held2;
		if (flag3)
		{
			Deliver(value, num, readOnlySpan2.ToArray(), nowMs, ref segments);
		}
		else
		{
			if (num > value.LastDelivered)
			{
				SortedDictionary<ulong, HeldFrame>? held = value.Held;
				if (held == null || !held.ContainsKey(num))
				{
					if (num == value.LastDelivered + 1)
					{
						held2 = value.Held;
						if (held2 != null && held2.Count > 0)
						{
							LateFills++;
						}
						Deliver(value, num, readOnlySpan2.ToArray(), nowMs, ref segments);
						DrainHeld(value, nowMs, ref segments);
					}
					else
					{
						Hold(value, num, readOnlySpan2.ToArray(), nowMs, ref segments);
					}
					goto IL_0351;
				}
			}
			Duplicates++;
			long? num3 = SkippedAtMs(value, num);
			if (num3.HasValue)
			{
				long valueOrDefault = num3.GetValueOrDefault();
				LostAfterSkip++;
				AnomalyObserved?.Invoke(key, Anomaly.LostAfterSkip, (int)Math.Clamp(nowMs - valueOrDefault, 0L, 2147483647L));
			}
		}
		goto IL_0351;
		IL_0351:
		held2 = value.Held;
		if (held2 != null && held2.Count > 0)
		{
			SkipDeadGaps(value, nowMs, force: false, ref segments);
		}
		return true;
	}

	private static bool IsKnownControlKind(int kind)
	{
		switch (kind)
		{
		case 1:
		case 2:
		case 4:
		case 7:
		case 8:
		case 10:
		case 11:
		case 12:
			return true;
		default:
			return false;
		}
	}

	private void Deliver(ConnectionState cs, ulong sequence, byte[] data, long arrivedMs, ref List<GameSegment>? segments)
	{
		cs.LastDelivered = sequence;
		(segments ?? (segments = new List<GameSegment>())).Add(new GameSegment(cs.Src, cs.Dst, data, !cs.DeliveredAny, arrivedMs, cs.Device));
		cs.DeliveredAny = true;
		Delivered++;
	}

	private void Hold(ConnectionState cs, ulong sequence, byte[] data, long nowMs, ref List<GameSegment>? segments)
	{
		ConnectionState connectionState = cs;
		(connectionState.Held ?? (connectionState.Held = new SortedDictionary<ulong, HeldFrame>()))[sequence] = new HeldFrame(data, nowMs);
		connectionState = cs;
		(connectionState.HeldOrder ?? (connectionState.HeldOrder = new Queue<(ulong, long)>())).Enqueue((sequence, nowMs));
		_heldBytes += data.Length;
		_heldFrames++;
		_holding.Add(cs);
		HeldFrames++;
		if (_heldBytes > MaxHeldBytesSeen)
		{
			MaxHeldBytesSeen = (int)Math.Min(2147483647L, _heldBytes);
		}
		while ((_heldBytes > 4194304 || _heldFrames > 4096) && _holding.Count > 0)
		{
			ConnectionState cs2 = null;
			long num = long.MaxValue;
			foreach (ConnectionState item in _holding)
			{
				long num2 = OldestArrival(item);
				if (num2 < num)
				{
					num = num2;
					cs2 = item;
				}
			}
			SkipDeadGaps(cs2, nowMs, force: true, ref segments);
		}
	}

	private void DrainHeld(ConnectionState cs, long nowMs, ref List<GameSegment>? segments)
	{
		SortedDictionary<ulong, HeldFrame> held = cs.Held;
		if (held == null)
		{
			return;
		}
		HeldFrame value;
		while (held.Count > 0 && held.Remove(cs.LastDelivered + 1, out value))
		{
			Release(value);
			if (nowMs - value.ArrivedMs > MaxHoldMs)
			{
				MaxHoldMs = nowMs - value.ArrivedMs;
			}
			Deliver(cs, cs.LastDelivered + 1, value.Data, value.ArrivedMs, ref segments);
		}
		if (held.Count <= 0)
		{
			cs.HeldOrder?.Clear();
			_holding.Remove(cs);
		}
	}

	private void SkipDeadGaps(ConnectionState cs, long nowMs, bool force, ref List<GameSegment>? segments)
	{
		do
		{
			SortedDictionary<ulong, HeldFrame> held = cs.Held;
			if (held != null && held.Count > 0)
			{
				ulong num = cs.LastDelivered + 1;
				if (!force && WaitingCanHelp(cs, num, nowMs))
				{
					break;
				}
				ulong num2 = FirstKey(held);
				GapsSkipped++;
				List<(ulong, ulong, long)> list = cs.SkippedRanges ?? (cs.SkippedRanges = new List<(ulong, ulong, long)>());
				if (list.Count >= 16)
				{
					list.RemoveAt(0);
				}
				list.Add((num, num2 - 1, nowMs));
				SkippedSequences += (long)(num2 - num);
				cs.LastDelivered = num2 - 1;
				DrainHeld(cs, nowMs, ref segments);
				continue;
			}
			break;
		}
		while (!force);
	}

	private static long? SkippedAtMs(ConnectionState cs, ulong sequence)
	{
		List<(ulong, ulong, long)> skippedRanges = cs.SkippedRanges;
		if (skippedRanges == null)
		{
			return null;
		}
		foreach (var (num, num2, value) in skippedRanges)
		{
			if (sequence >= num && sequence <= num2)
			{
				return value;
			}
		}
		return null;
	}

	private static bool WaitingCanHelp(ConnectionState cs, ulong missing, long nowMs)
	{
		if (nowMs - OldestArrival(cs) >= 5000)
		{
			return false;
		}
		foreach (LegState leg in cs.Legs)
		{
			if (leg.MaxSequence < missing && nowMs - leg.LastSeenMs < 5000)
			{
				return true;
			}
		}
		return false;
	}

	private void SweepHolds(long nowMs, ref List<GameSegment>? segments)
	{
		_sweep.Clear();
		_sweep.AddRange(_holding);
		foreach (ConnectionState item in _sweep)
		{
			SkipDeadGaps(item, nowMs, force: false, ref segments);
		}
	}

	private void Release(HeldFrame frame)
	{
		_heldBytes -= frame.Data.Length;
		_heldFrames--;
	}

	private static long OldestArrival(ConnectionState cs)
	{
		Queue<(ulong, long)> heldOrder = cs.HeldOrder;
		if (heldOrder == null || cs.Held == null)
		{
			return long.MaxValue;
		}
		while (heldOrder.Count > 0 && !cs.Held.ContainsKey(heldOrder.Peek().Item1))
		{
			heldOrder.Dequeue();
		}
		if (heldOrder.Count <= 0)
		{
			return long.MaxValue;
		}
		return heldOrder.Peek().Item2;
	}

	private static ulong FirstKey(SortedDictionary<ulong, HeldFrame> held)
	{
		using SortedDictionary<ulong, HeldFrame>.Enumerator enumerator = held.GetEnumerator();
		enumerator.MoveNext();
		return enumerator.Current.Key;
	}

	private static void NoteLeg(ConnectionState cs, StreamKey key, ulong sequence, long nowMs)
	{
		LegState legState = null;
		foreach (LegState leg in cs.Legs)
		{
			if (!(leg.Key != key))
			{
				legState = leg;
				break;
			}
		}
		if (legState == null)
		{
			if (cs.Legs.Count < 8)
			{
				legState = new LegState
				{
					Key = key
				};
				cs.Legs.Add(legState);
			}
			else
			{
				legState = cs.Legs[0];
				foreach (LegState leg2 in cs.Legs)
				{
					if (leg2.LastSeenMs < legState.LastSeenMs)
					{
						legState = leg2;
					}
				}
				legState.Key = key;
				legState.MaxSequence = 0uL;
			}
		}
		if (sequence > legState.MaxSequence)
		{
			legState.MaxSequence = sequence;
		}
		legState.LastSeenMs = nowMs;
	}

	private static bool TryWalk(ReadOnlySpan<byte> body, out Frame frame)
	{
		int kind = -1;
		int direction = -1;
		int connectionOffset = 0;
		int connectionLength = -1;
		int sequenceOffset = 0;
		int sequenceLength = -1;
		int gameOffset = 0;
		int gameLength = -1;
		int num = 0;
		while (num < body.Length)
		{
			if (num + 2 > body.Length)
			{
				frame = default;
				return false;
			}
			byte b = body[num];
			byte b2 = body[num + 1];
			int num2;
			int num3;
			switch (b)
			{
			case 1:
				num += 2;
				continue;
			case 5:
				if (num + 3 > body.Length)
				{
					frame = default;
					return false;
				}
				num2 = num + 3;
				num3 = body[num + 2];
				break;
			case 9:
				if (num + 4 > body.Length)
				{
					frame = default;
					return false;
				}
				num2 = num + 4;
				num3 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num + 2, 2));
				break;
			default:
				frame = default;
				return false;
			}
			if (num2 + num3 > body.Length)
			{
				frame = default;
				return false;
			}
			switch (b2)
			{
			case 1:
				if (num3 == 1)
				{
					kind = body[num2];
				}
				break;
			case 2:
				if (num3 == 1)
				{
					direction = body[num2];
				}
				break;
			case 3:
				connectionOffset = num2;
				connectionLength = num3;
				break;
			case 34:
				sequenceOffset = num2;
				sequenceLength = num3;
				break;
			case 35:
				gameOffset = num2;
				gameLength = num3;
				break;
			}
			num = num2 + num3;
		}
		frame = new Frame(kind, direction, connectionOffset, connectionLength, sequenceOffset, sequenceLength, gameOffset, gameLength);
		return true;
	}

	private StreamState Track(StreamKey key)
	{
		StreamState streamState = null;
		if (_streams.Count >= 256)
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
			if (_streams.Remove(key2, out StreamState value))
			{
				StreamsEvicted++;
				streamState = value;
			}
		}
		StreamState streamState4 = streamState ?? new StreamState();
		streamState4.Len = 0;
		streamState4.Synced = true;
		streamState4.LastTouch = ++_touch;
		_streams[key] = streamState4;
		return streamState4;
	}

	private ConnectionState TrackConnection(ConnectionKey ck, StreamKey firstLeg, long nowMs, ref List<GameSegment>? segments)
	{
		if (_connections.Count >= 256)
		{
			ConnectionKey key = default;
			ConnectionState connectionState = null;
			long num = long.MaxValue;
			foreach (var (connectionKey2, connectionState3) in _connections)
			{
				if (connectionState3.LastTouch < num)
				{
					num = connectionState3.LastTouch;
					key = connectionKey2;
					connectionState = connectionState3;
				}
			}
			if (connectionState != null)
			{
				while (true)
				{
					SortedDictionary<ulong, HeldFrame> held = connectionState.Held;
					if (held == null || held.Count <= 0)
					{
						break;
					}
					SkipDeadGaps(connectionState, nowMs, force: true, ref segments);
				}
				_holding.Remove(connectionState);
				_connections.Remove(key);
				ConnectionsEvicted++;
			}
		}
		ConnectionState connectionState4 = new ConnectionState
		{
			Device = firstLeg.Device,
			Src = firstLeg.Src,
			Dst = firstLeg.Dst,
			LastTouch = ++_touch
		};
		_connections[ck] = connectionState4;
		ConnectionsSeen++;
		return connectionState4;
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
}
