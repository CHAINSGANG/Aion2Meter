using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class GearDetailParser
{
	public sealed record DetailRecord(int ItemId, IReadOnlyList<GearSubStat> SubStats);

	private const int MaxSubStats = 7;

	public static DetailRecord? Parse(ReadOnlySpan<byte> body, IReadOnlySet<int> itemIds)
	{
		if (itemIds == null)
		{
			throw new ArgumentNullException("itemIds");
		}
		if (body.Length < 64)
		{
			return null;
		}
		int num = FindFirstItemId(body, itemIds);
		if (num == 0)
		{
			return null;
		}
		IReadOnlyList<GearSubStat> subStats = ParseSubStats(body);
		return new DetailRecord(num, subStats);
	}

	public static IReadOnlyList<GearSubStat> ParseSubStats(ReadOnlySpan<byte> body)
	{
		var (num, num2) = FindSubStatBlockStart(body);
		if (num < 0)
		{
			return Array.Empty<GearSubStat>();
		}
		GearSubStat[] array = new GearSubStat[num2];
		for (int i = 0; i < num2; i++)
		{
			int num3 = num + 1 + i * 6;
			ushort statId = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num3, 2));
			ushort value = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num3 + 2, 2));
			array[i] = new GearSubStat(statId, value);
		}
		return array;
	}

	private static int FindFirstItemId(ReadOnlySpan<byte> body, IReadOnlySet<int> itemIds)
	{
		for (int i = 0; i + 4 <= body.Length; i++)
		{
			int num = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(i, 4));
			if (itemIds.Contains(num))
			{
				return num;
			}
		}
		return 0;
	}

	private static (int Offset, int Count) FindSubStatBlockStart(ReadOnlySpan<byte> body)
	{
		for (int i = 0; i + 1 < body.Length; i++)
		{
			byte b = body[i];
			if (b < 1 || b > 7 || i + 1 + b * 6 > body.Length)
			{
				continue;
			}
			bool flag = true;
			for (int j = 0; j < b; j++)
			{
				int num = i + 1 + j * 6;
				ushort num2 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num, 2));
				ushort num3 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num + 2, 2));
				ushort num4 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num + 4, 2));
				if (num2 < 1 || num2 > 1000 || num3 < 1 || num3 > 50000 || num4 != 0)
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return (Offset: i, Count: b);
			}
		}
		return (Offset: -1, Count: 0);
	}
}
