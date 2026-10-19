using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AionFlex.Core.Parsing;

public static class WingActiveLevelsParser
{
	private static readonly byte[] Header = new byte[5] { 1, 0, 0, 0, 0 };

	private const int EntrySize = 5;

	private const int MaxCount = 16;

	public static int? FindEquippedWingId(ReadOnlySpan<byte> bundle, IReadOnlySet<int>? validWingIds)
	{
		if (validWingIds == null || validWingIds.Count == 0)
		{
			return null;
		}
		for (int i = 0; i + Header.Length + 1 <= bundle.Length; i++)
		{
			bool flag = true;
			for (int j = 0; j < Header.Length; j++)
			{
				if (bundle[i + j] != Header[j])
				{
					flag = false;
					break;
				}
			}
			if (!flag)
			{
				continue;
			}
			int num = i + Header.Length;
			int num2 = bundle[num];
			if (num2 < 1 || num2 > 16)
			{
				continue;
			}
			int num3 = num + 1;
			if (num3 + num2 * 5 > bundle.Length)
			{
				continue;
			}
			int value = 0;
			bool flag2 = true;
			for (int k = 0; k < num2; k++)
			{
				int num4 = num3 + k * 5;
				if (bundle[num4] != k + 1)
				{
					flag2 = false;
					break;
				}
				int num5 = (int)BinaryPrimitives.ReadUInt32LittleEndian(bundle.Slice(num4 + 1, 4));
				if (!validWingIds.Contains(num5))
				{
					flag2 = false;
					break;
				}
				if (k == 0)
				{
					value = num5;
				}
			}
			if (flag2)
			{
				return value;
			}
		}
		return null;
	}
}
