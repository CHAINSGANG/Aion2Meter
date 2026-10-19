using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing;

public sealed class GameStreamReassembler
{
	private class DirectionBuffer
	{
		public byte[] Buf = new byte[32768];

		public int RPos;

		public int WPos;

		public bool IsGame;

		public bool Pruned;

		public long TotalBytes;

		public int SegsSinceExtract;

		public int Length => WPos - RPos;

		public void Append(byte[] data)
		{
			SegsSinceExtract++;
			TotalBytes += data.Length;
			if (WPos + data.Length > Buf.Length)
			{
				Compact();
				if (WPos + data.Length > Buf.Length)
				{
					int num = Math.Max(Buf.Length * 2, WPos + data.Length);
					if (num > 524288)
					{
						RPos = 0;
						WPos = 0;
						return;
					}
					byte[] array = new byte[num];
					Buffer.BlockCopy(Buf, 0, array, 0, WPos);
					Buf = array;
				}
			}
			Buffer.BlockCopy(data, 0, Buf, WPos, data.Length);
			WPos += data.Length;
		}

		public void Compact()
		{
			if (RPos > 0)
			{
				int length = Length;
				Buffer.BlockCopy(Buf, RPos, Buf, 0, length);
				RPos = 0;
				WPos = length;
			}
		}
	}

	private readonly ILogger _logger;

	private readonly ConcurrentDictionary<long, DirectionBuffer> _directions = new ConcurrentDictionary<long, DirectionBuffer>();

	private long _lastDiagTicks;

	private volatile int _diagExtracted;

	private volatile int _diagReassembled;

	private volatile int _diagSegments;

	private volatile int _diagPruned;

	public int DiagReassembled => _diagReassembled;

	public int DiagDirect => _diagExtracted - _diagReassembled;

	public int DiagSkipped => _diagPruned;

	public int DiagStallReset => 0;

	public int PendingBytes => 0;

	public event Action<byte[]>? PacketReady;

	public GameStreamReassembler(ILogger logger)
	{
		_logger = logger;
	}

	public void AddSegment(int srcPort, int dstPort, byte[] data)
	{
		if (data.Length == 0)
		{
			return;
		}
		_diagSegments++;
		long key = ((long)srcPort << 32) | (uint)dstPort;
		DirectionBuffer orAdd = _directions.GetOrAdd(key, (long _) => new DirectionBuffer());
		if (orAdd.Pruned)
		{
			return;
		}
		orAdd.Append(data);
		ExtractPackets(orAdd);
		if (!orAdd.IsGame && orAdd.TotalBytes > 16384)
		{
			orAdd.Pruned = true;
			_diagPruned++;
			return;
		}
		long tickCount = Environment.TickCount64;
		if (tickCount - _lastDiagTicks <= 10000)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (DirectionBuffer value in _directions.Values)
		{
			if (!value.Pruned)
			{
				num++;
			}
			if (value.IsGame)
			{
				num2++;
			}
		}
		_logger.LogDebug("\ud83d\udd04 STREAM: segments={Seg}, extracted={Ext}, reassembled={Reasm}, pruned={Pruned}, active={Active}, game={Game}, pending={Pend}B", _diagSegments, _diagExtracted, _diagReassembled, _diagPruned, num, num2, orAdd.Length);
		_lastDiagTicks = tickCount;
	}

	public void AddData(byte[] data)
	{
		if (data.Length != 0)
		{
			PacketReady?.Invoke(data);
		}
	}

	private void ExtractPackets(DirectionBuffer dir)
	{
		while (dir.Length >= 3)
		{
			var (num, num2) = ReadVarInt(dir.Buf, dir.RPos, dir.WPos);
			if (num2 <= 0)
			{
				break;
			}
			if (num < 3 || num > 16384)
			{
				dir.RPos++;
				continue;
			}
			if (dir.Length < num)
			{
				break;
			}
			byte[] array = new byte[num];
			Buffer.BlockCopy(dir.Buf, dir.RPos, array, 0, num);
			dir.RPos += num;
			if (num > 3)
			{
				dir.RPos -= 3;
			}
			if (!dir.IsGame && num2 + 1 < num)
			{
				byte b = array[num2 + 1];
				if (b == 56 || b == 55 || b == 54 || b == 141)
				{
					dir.IsGame = true;
				}
			}
			if (dir.SegsSinceExtract > 1)
			{
				_diagReassembled++;
			}
			_diagExtracted++;
			dir.SegsSinceExtract = 0;
			PacketReady?.Invoke(array);
		}
		dir.Compact();
	}

	public void Reset()
	{
		_directions.Clear();
	}

	public void ResetDiagnostics()
	{
		_diagExtracted = 0;
		_diagReassembled = 0;
		_diagSegments = 0;
		_diagPruned = 0;
	}

	internal static (int Value, int Size) ReadVarInt(byte[] data, int offset, int limit)
	{
		int num = 0;
		int num2 = 0;
		int num3 = offset;
		while (num3 < limit)
		{
			byte b = data[num3++];
			num |= (b & 0x7F) << num2;
			if ((b & 0x80) == 0)
			{
				return (Value: num, Size: num3 - offset);
			}
			num2 += 7;
			if (num2 >= 32)
			{
				return (Value: -1, Size: -1);
			}
		}
		return (Value: -1, Size: -1);
	}
}
