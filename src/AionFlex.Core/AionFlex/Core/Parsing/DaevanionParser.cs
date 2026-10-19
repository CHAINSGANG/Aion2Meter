using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class DaevanionParser
{
	public const int OpcodeByte0 = 36;

	public const int OpcodeByte1 = 226;

	public static bool TryParseBody(ReadOnlySpan<byte> body, out DaevanionSnapshot snapshot)
	{
		snapshot = DaevanionSnapshot.Empty;
		if (body.Length < 4)
		{
			return false;
		}
		if (body[0] != 36 || body[1] != 226)
		{
			return false;
		}
		int num = 2;
		byte b = body[num];
		num++;
		if (b == 0 || b > 32)
		{
			return false;
		}
		List<DaevanionBoardEntry> list = new List<DaevanionBoardEntry>(b);
		for (int i = 0; i < b; i++)
		{
			if (num + 4 > body.Length)
			{
				return false;
			}
			int boardId = (int)BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(num, 4));
			num += 4;
			if (!TryReadVarInt(body, ref num, out var value))
			{
				return false;
			}
			if (value < 0 || value > 1000)
			{
				return false;
			}
			if (num + 4 * value > body.Length)
			{
				return false;
			}
			int[] array = new int[value];
			for (int j = 0; j < value; j++)
			{
				array[j] = (int)BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(num, 4));
				num += 4;
			}
			list.Add(new DaevanionBoardEntry(boardId, array));
		}
		snapshot = new DaevanionSnapshot(list);
		return true;
	}

	public static IReadOnlyList<DaevanionSnapshot> ScanAll(ReadOnlySpan<byte> payload)
	{
		IReadOnlyList<int> anchoredButFailedOffsets;
		return ScanAll(payload, out anchoredButFailedOffsets);
	}

	public static IReadOnlyList<DaevanionSnapshot> ScanAll(ReadOnlySpan<byte> payload, out IReadOnlyList<int> anchoredButFailedOffsets)
	{
		List<DaevanionSnapshot> list = new List<DaevanionSnapshot>(2);
		List<int> list2 = null;
		int num = payload.Length - 4;
		for (int i = 0; i < num; i++)
		{
			if (payload[i] != 36 || payload[i + 1] != 226)
			{
				continue;
			}
			byte b = payload[i + 2];
			if (b != 0 && b <= 8 && i + 7 <= payload.Length && IsValidClassBaseByte(payload[i + 3]) && payload[i + 4] == 0 && payload[i + 5] == 0 && payload[i + 6] == 0)
			{
				if (!TryParseBody(payload.Slice(i), out DaevanionSnapshot snapshot))
				{
					(list2 ?? (list2 = new List<int>())).Add(i);
				}
				else
				{
					list.Add(snapshot);
				}
			}
		}
		IReadOnlyList<int> readOnlyList = list2;
		anchoredButFailedOffsets = readOnlyList ?? Array.Empty<int>();
		return list;
	}

	private static bool IsValidClassBaseByte(byte b)
	{
		return b switch
		{
			11 => true, 
			21 => true, 
			31 => true, 
			41 => true, 
			51 => true, 
			61 => true, 
			71 => true, 
			81 => true, 
			_ => false, 
		};
	}

	private static bool TryReadVarInt(ReadOnlySpan<byte> buf, ref int pos, out int value)
	{
		value = 0;
		int num = 0;
		while (pos < buf.Length)
		{
			byte b = buf[pos];
			pos++;
			value |= (b & 0x7F) << num;
			if ((b & 0x80) == 0)
			{
				return true;
			}
			num += 7;
			if (num > 28)
			{
				return false;
			}
		}
		return false;
	}
}
