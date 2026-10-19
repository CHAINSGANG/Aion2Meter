using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class WingRosterParser
{
	private const int WingIdMin = 30000000;

	private const int WingIdMax = 41000000;

	private const int LevelMax = 10;

	private const int EntrySize = 5;

	private const int MinRunLength = 3;

	public static WingSnapshot Parse(ReadOnlySpan<byte> bundle, IReadOnlySet<int>? validWingIds = null)
	{
		int longestRun;
		int longestRunStart;
		return Parse(bundle, validWingIds, out longestRun, out longestRunStart);
	}

	public static WingSnapshot Parse(ReadOnlySpan<byte> bundle, IReadOnlySet<int>? validWingIds, out int longestRun, out int longestRunStart)
	{
		int num = -1;
		int num2 = 0;
		int num3 = 0;
		while (num3 + 5 <= bundle.Length)
		{
			if (IsValidEntry(bundle, num3, validWingIds))
			{
				int num4 = num3;
				int num5 = 0;
				while (num3 + 5 <= bundle.Length && IsValidEntry(bundle, num3, validWingIds))
				{
					num3 += 5;
					num5++;
				}
				if (num5 > num2)
				{
					num = num4;
					num2 = num5;
				}
			}
			else
			{
				num3++;
			}
		}
		longestRun = num2;
		longestRunStart = num;
		if (num < 0 || num2 < 3)
		{
			return WingSnapshot.Empty;
		}
		WingEntry[] array = new WingEntry[num2];
		for (int i = 0; i < num2; i++)
		{
			int num6 = num + i * 5;
			int wingId = (int)BinaryPrimitives.ReadUInt32LittleEndian(bundle.Slice(num6, 4));
			byte level = bundle[num6 + 4];
			array[i] = new WingEntry(wingId, level);
		}
		return new WingSnapshot(array);
	}

	private static bool IsValidEntry(ReadOnlySpan<byte> data, int off, IReadOnlySet<int>? validWingIds)
	{
		int num = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(off, 4));
		if (num < 30000000 || num > 41000000)
		{
			return false;
		}
		if (data[off + 4] > 10)
		{
			return false;
		}
		if (validWingIds != null && !validWingIds.Contains(num))
		{
			return false;
		}
		return true;
	}
}
