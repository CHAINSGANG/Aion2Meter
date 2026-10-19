using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class GenusInsightParser
{
	private const byte Header0 = 3;

	private const byte Header1 = 1;

	private const byte Header3 = 0;

	private const int HeaderBytes = 4;

	private const byte MinHeaderCount = 1;

	private const byte MaxHeaderCount = 9;

	public const int ExpectedGenusCount = 5;

	public const int SlotsPerGenus = 9;

	private const int MaxSlotPadding = 4;

	public static GenusInsightSnapshot Parse(ReadOnlySpan<byte> bundle)
	{
		IReadOnlyList<int> anchoredButFailedOffsets;
		return Parse(bundle, out anchoredButFailedOffsets);
	}

	public static GenusInsightSnapshot Parse(ReadOnlySpan<byte> bundle, out IReadOnlyList<int> anchoredButFailedOffsets)
	{
		List<GenusBlock> list = new List<GenusBlock>(5);
		List<int> list2 = null;
		int num = 0;
		int num2 = bundle.Length - 4;
		int num3 = 0;
		while (num <= num2 && num3 < 5)
		{
			IReadOnlyList<GenusEffect> effects;
			int consumed;
			if (!IsHeader(bundle, num))
			{
				num++;
			}
			else if (!TryParseBlock(bundle, num, out effects, out consumed))
			{
				if (IsAnchoredWithPlausibleSlot0Grade(bundle, num))
				{
					(list2 ?? (list2 = new List<int>())).Add(num + 4);
				}
				num++;
			}
			else
			{
				list.Add(new GenusBlock((GenusType)num3, bundle[num + 1], effects));
				num3++;
				num += consumed;
			}
		}
		IReadOnlyList<int> readOnlyList = list2;
		anchoredButFailedOffsets = readOnlyList ?? Array.Empty<int>();
		if (list.Count != 0)
		{
			return new GenusInsightSnapshot(list);
		}
		return GenusInsightSnapshot.Empty;
	}

	private static bool IsAnchoredWithPlausibleSlot0Grade(ReadOnlySpan<byte> data, int headerPos)
	{
		if (!IsHeader(data, headerPos))
		{
			return false;
		}
		int num = headerPos + 4;
		if (num >= data.Length)
		{
			return false;
		}
		return IsPlausibleGrade(data[num]);
	}

	private static bool IsHeader(ReadOnlySpan<byte> data, int pos)
	{
		if (pos + 4 <= data.Length && data[pos] == 3 && data[pos + 1] == 1 && data[pos + 2] >= 1 && data[pos + 2] <= 9)
		{
			return data[pos + 3] == 0;
		}
		return false;
	}

	private static bool TryParseBlock(ReadOnlySpan<byte> data, int headerPos, out IReadOnlyList<GenusEffect> effects, out int consumed)
	{
		effects = Array.Empty<GenusEffect>();
		consumed = 0;
		int num = headerPos + 4;
		if (num + 11 > data.Length)
		{
			return false;
		}
		List<GenusEffect> list = new List<GenusEffect>(9);
		byte b = data[num];
		if (!IsPlausibleGrade(b))
		{
			return false;
		}
		ushort optionId = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(num + 1, 2));
		int num2 = (int)BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(num + 3, 8));
		if (!IsPlausibleValue(num2))
		{
			return false;
		}
		list.Add(new GenusEffect(0, b, optionId, num2));
		num += 11;
		for (int i = 1; i <= 8; i++)
		{
			int j = 0;
			bool flag = false;
			for (; j <= 4 && num + j + 12 <= data.Length; j++)
			{
				if (data[num + j] == i && IsPlausibleGrade(data[num + j + 1]) && IsPlausibleValue((int)BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(num + j + 4, 8))))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				break;
			}
			num += j;
			byte slotIndex = data[num];
			byte grade = data[num + 1];
			ushort optionId2 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(num + 2, 2));
			int value = (int)BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(num + 4, 8));
			list.Add(new GenusEffect(slotIndex, grade, optionId2, value));
			num += 12;
		}
		effects = list;
		consumed = num - headerPos;
		return true;
	}

	private static bool IsPlausibleGrade(byte b)
	{
		if (b >= 1)
		{
			return b <= 6;
		}
		return false;
	}

	private static bool IsPlausibleValue(int v)
	{
		if (v >= 0)
		{
			return v < 100000;
		}
		return false;
	}
}
