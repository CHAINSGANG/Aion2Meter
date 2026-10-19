using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class FieldBossListDecoder
{
	public const byte Opcode0 = 1;

	public const byte Opcode1 = 145;

	public const int MaxEntries = 100;

	public const long MinTimeMs = 1700000000000L;

	public const long MaxTimeMs = 1900000000000L;

	private const int HeaderBytes = 7;

	private const int TrailerBytes = 3;

	public static bool TryDecode(ReadOnlySpan<byte> buffer, out FieldBossList? list, out FieldBossListReject reject)
	{
		list = null;
		reject = FieldBossListReject.NotAList;
		try
		{
			return Decode(buffer, out list, out reject);
		}
		catch (Exception)
		{
			list = null;
			reject = FieldBossListReject.Length;
			return false;
		}
	}

	private static bool Decode(ReadOnlySpan<byte> buffer, out FieldBossList? list, out FieldBossListReject reject)
	{
		list = null;
		int offset = 0;
		if (!TryReadVarInt(buffer, ref offset, buffer.Length, out var value))
		{
			reject = FieldBossListReject.NotAList;
			return false;
		}
		int num = offset;
		if (buffer.Length < num + 2 + 2 || buffer[num] != 1 || buffer[num + 1] != 145 || buffer[num + 2] != 0 || buffer[num + 3] != 0)
		{
			reject = FieldBossListReject.NotAList;
			return false;
		}
		int num2 = num + value - 4;
		if (num2 < num + 2 + 7 + 3 || num2 > buffer.Length)
		{
			reject = FieldBossListReject.Length;
			return false;
		}
		offset = num + 2 + 2;
		int num3 = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset, 4));
		offset += 4;
		if (num3 <= 0)
		{
			reject = FieldBossListReject.NotAList;
			return false;
		}
		int num4 = buffer[offset++];
		if (num4 < 1 || num4 > 100)
		{
			reject = FieldBossListReject.Count;
			return false;
		}
		List<FieldBossListEntry> list2 = new List<FieldBossListEntry>(num4);
		for (int i = 0; i < num4; i++)
		{
			if (offset >= num2)
			{
				reject = FieldBossListReject.Trailer;
				return false;
			}
			byte b = buffer[offset++];
			if (b > 1)
			{
				reject = FieldBossListReject.State;
				return false;
			}
			if (!TryReadVarInt(buffer, ref offset, num2, out var value2) || value2 <= 0)
			{
				reject = FieldBossListReject.RowId;
				return false;
			}
			FieldBossPosition? position = null;
			if (b == 1)
			{
				if (offset + 12 > num2)
				{
					reject = FieldBossListReject.Position;
					return false;
				}
				float num5 = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset, 4));
				float num6 = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4, 4));
				float num7 = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 8, 4));
				if (!float.IsFinite(num5) || !float.IsFinite(num6) || !float.IsFinite(num7))
				{
					reject = FieldBossListReject.Position;
					return false;
				}
				position = new FieldBossPosition(num5, num6, num7);
				offset += 12;
			}
			byte? unexplainedByte = null;
			if (!TryReadTime(buffer, offset, num2, out var time))
			{
				if (!TryReadTime(buffer, offset + 1, num2, out time))
				{
					reject = FieldBossListReject.Time;
					return false;
				}
				unexplainedByte = buffer[offset];
				offset++;
			}
			offset += 8;
			list2.Add(new FieldBossListEntry(new FieldBossEntry(value2, b == 1, position, time), unexplainedByte));
		}
		if (offset + 3 != num2 || buffer[offset] != 0 || buffer[offset + 1] != 0 || buffer[offset + 2] != 0)
		{
			reject = FieldBossListReject.Trailer;
			return false;
		}
		reject = FieldBossListReject.None;
		list = new FieldBossList(num3, list2);
		return true;
	}

	private static bool TryReadTime(ReadOnlySpan<byte> buffer, int offset, int end, out long time)
	{
		time = 0L;
		if (offset < 0 || offset + 8 > end)
		{
			return false;
		}
		ulong num = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset, 8));
		if (num < 1700000000000L || num > 1900000000000L)
		{
			return false;
		}
		time = (long)num;
		return true;
	}

	private static bool TryReadVarInt(ReadOnlySpan<byte> buffer, ref int offset, int end, out int value)
	{
		value = 0;
		for (int i = 0; i < 4; i++)
		{
			if (offset >= end)
			{
				return false;
			}
			byte b = buffer[offset++];
			value |= (b & 0x7F) << 7 * i;
			if ((b & 0x80) == 0)
			{
				return true;
			}
		}
		return false;
	}
}
