using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class GearChangeParser
{
	private const byte Separator = 14;

	private const int MaxSlotIndex = 23;

	private const int MaxEnchantLevel = 31;

	public static GearChange? Parse(ReadOnlySpan<byte> body, IReadOnlySet<int> itemIds)
	{
		if (itemIds == null)
		{
			throw new ArgumentNullException("itemIds");
		}
		if (body.Length < 12)
		{
			return null;
		}
		int num = FindItemIdOffset(body, 0, itemIds);
		if (num < 0)
		{
			return null;
		}
		GearItem? oldItem = ParseEntry(body, num, itemIds);
		if (!oldItem.HasValue)
		{
			return null;
		}
		int num2 = num + 4;
		int num3 = FindItemIdOffset(body, num2, itemIds);
		GearItem? newItem = null;
		if (num3 > num2)
		{
			bool flag = false;
			for (int i = num2; i < num3; i++)
			{
				if (body[i] == 14)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				newItem = ParseEntry(body, num3, itemIds);
			}
		}
		if (!TryReadVarInt(body, 0, out var value, out var _))
		{
			return null;
		}
		return new GearChange(value, oldItem, newItem);
	}

	private static int FindItemIdOffset(ReadOnlySpan<byte> body, int from, IReadOnlySet<int> itemIds)
	{
		for (int i = from; i + 6 <= body.Length; i++)
		{
			int item = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(i, 4));
			if (itemIds.Contains(item))
			{
				byte b = body[i + 4];
				int num = body[i + 5];
				if (b <= 31 && num <= 23)
				{
					return i;
				}
			}
		}
		return -1;
	}

	private static GearItem? ParseEntry(ReadOnlySpan<byte> body, int off, IReadOnlySet<int> itemIds)
	{
		if (off + 6 > body.Length)
		{
			return null;
		}
		int num = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(off, 4));
		if (!itemIds.Contains(num))
		{
			return null;
		}
		int num2 = body[off + 4];
		int num3 = body[off + 5];
		if (num2 > 31 || num3 > 23)
		{
			return null;
		}
		int num4 = off + 6;
		int num5 = body.Length;
		for (int i = num4; i + 4 <= body.Length; i++)
		{
			if (body[i] == 14)
			{
				num5 = i;
				break;
			}
			int item = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(i, 4));
			if (itemIds.Contains(item))
			{
				num5 = i;
				break;
			}
		}
		byte[] metaRaw = body.Slice(num4, num5 - num4).ToArray();
		return new GearItem(num, num2, num3, metaRaw, Array.Empty<GearSubStat>(), Array.Empty<GearSkillSubStat>());
	}

	private static bool TryReadVarInt(ReadOnlySpan<byte> buf, int pos, out int value, out int bytesRead)
	{
		value = 0;
		bytesRead = 0;
		int num = 0;
		int num2 = pos;
		while (num2 < buf.Length)
		{
			byte b = buf[num2++];
			value |= (b & 0x7F) << num;
			bytesRead++;
			if ((b & 0x80) == 0)
			{
				return true;
			}
			num += 7;
			if (num > 35)
			{
				return false;
			}
		}
		return false;
	}
}
