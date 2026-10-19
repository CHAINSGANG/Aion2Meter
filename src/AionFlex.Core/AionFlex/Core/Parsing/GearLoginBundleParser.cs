using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class GearLoginBundleParser
{
	public sealed record BundleItem(int ItemId, int SlotByte, int EnchantLevel, IReadOnlyList<GearSubStat> SubStats, IReadOnlyList<GearSkillSubStat> SkillSubStats)
	{
		public int SlotZeroIndexed => SlotByte - 1;
	}

	private const byte EquippedFlag = 11;

	private const int MaxEnchant = 30;

	private const int MaxSubStats = 7;

	private const int MaxSkillSubStats = 8;

	private const int MinSkillId = 10000000;

	private const int MaxSkillId = 30000000;

	private const int MaxSkillLevel = 10;

	private const int SkillIdGranularity = 10000;

	public static IReadOnlyList<BundleItem> Parse(ReadOnlySpan<byte> body, IReadOnlySet<int> itemIds)
	{
		if (itemIds == null)
		{
			throw new ArgumentNullException("itemIds");
		}
		List<int> list = new List<int>(32);
		for (int i = 0; i + 32 <= body.Length; i++)
		{
			int item = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(i, 4));
			if (itemIds.Contains(item) && body[i + 12] == 11)
			{
				list.Add(i);
			}
		}
		List<BundleItem> list2 = new List<BundleItem>(list.Count);
		for (int j = 0; j < list.Count; j++)
		{
			int num = list[j];
			int itemId = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(num, 4));
			byte slotByte = body[num + 13];
			int num2 = num + 26;
			int num3 = 0;
			if (num + 18 < body.Length)
			{
				if (body[num + 17] == 65)
				{
					byte b = body[num + 18];
					if (b > 0 && b <= 24 && num + 19 + b <= body.Length && IsAsciiNameRun(body, num + 19, b))
					{
						num3 = b;
					}
				}
				else
				{
					byte b2 = body[num + 17];
					if (b2 > 0 && b2 <= 24 && num + 18 + b2 <= body.Length && IsAsciiNameRun(body, num + 18, b2))
					{
						num3 = b2;
					}
				}
			}
			if (num3 > 0)
			{
				num2 += num3;
			}
			ushort num4 = (ushort)((num2 + 1 < body.Length) ? BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num2, 2)) : 0);
			if (num4 > 30)
			{
				num4 = (ushort)((num2 + 1 < body.Length) ? body[num2 + 1] : 0);
				if (num4 > 30)
				{
					num4 = body[num2];
					if (num4 > 30)
					{
						num4 = 0;
					}
				}
			}
			int num5 = ((j + 1 < list.Count) ? list[j + 1] : body.Length);
			int searchEnd = Math.Min(num + 200, Math.Max(num + 28, num5 - 4));
			int skillStart = FindSkillBlockStart(body, num + 28, searchEnd);
			int skillEnd = FindSkillBlockEnd(body, skillStart, searchEnd);
			IReadOnlyList<GearSubStat> subStats = FindSubStatBlockSkipping(body, num + 28, searchEnd, skillStart, skillEnd);
			IReadOnlyList<GearSkillSubStat> skillSubStats = FindSkillSubStatBlock(body, num + 28, searchEnd);
			list2.Add(new BundleItem(itemId, slotByte, num4, subStats, skillSubStats));
		}
		return list2;
	}

	private static bool IsAsciiNameRun(ReadOnlySpan<byte> body, int start, int length)
	{
		if (start < 0 || start + length > body.Length)
		{
			return false;
		}
		for (int i = 0; i < length; i++)
		{
			byte b = body[start + i];
			if (b < 32 || b > 126)
			{
				return false;
			}
		}
		return true;
	}

	private static int FindSkillBlockStart(ReadOnlySpan<byte> body, int searchStart, int searchEnd)
	{
		searchEnd = Math.Min(searchEnd, body.Length);
		for (int i = searchStart; i + 1 < searchEnd; i++)
		{
			if (IsValidSkillBlockAt(body, i))
			{
				return i;
			}
		}
		return searchEnd;
	}

	private static int FindSkillBlockEnd(ReadOnlySpan<byte> body, int skillStart, int searchEnd)
	{
		searchEnd = Math.Min(searchEnd, body.Length);
		int i;
		byte b;
		for (i = skillStart; i + 1 < searchEnd && IsValidSkillBlockAt(body, i); i += 1 + b * 5)
		{
			b = body[i];
		}
		return i;
	}

	private static bool IsValidSkillBlockAt(ReadOnlySpan<byte> body, int off)
	{
		if (off + 1 >= body.Length)
		{
			return false;
		}
		byte b = body[off];
		if (b < 1 || b > 8)
		{
			return false;
		}
		if (off + 1 + b * 5 > body.Length)
		{
			return false;
		}
		bool result = false;
		for (int i = 0; i < b; i++)
		{
			int num = off + 1 + i * 5;
			int num2 = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(num, 4));
			byte b2 = body[num + 4];
			if (num2 < 10000000 || num2 > 30000000 || b2 > 10 || num2 % 10000 != 0)
			{
				return false;
			}
			if (b2 >= 1)
			{
				result = true;
			}
		}
		return result;
	}

	private static IReadOnlyList<GearSkillSubStat> FindSkillSubStatBlock(ReadOnlySpan<byte> body, int searchStart, int searchEnd)
	{
		searchEnd = Math.Min(searchEnd, body.Length);
		List<GearSkillSubStat> list = new List<GearSkillSubStat>(8);
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		int num = searchStart;
		while (num + 1 < searchEnd)
		{
			byte b = body[num];
			if (b < 1 || b > 8 || num + 1 + b * 5 > body.Length)
			{
				num++;
				continue;
			}
			bool flag = true;
			bool flag2 = false;
			GearSkillSubStat[] array = new GearSkillSubStat[b];
			for (int i = 0; i < b; i++)
			{
				int num2 = num + 1 + i * 5;
				int num3 = BinaryPrimitives.ReadInt32LittleEndian(body.Slice(num2, 4));
				byte b2 = body[num2 + 4];
				if (num3 < 10000000 || num3 > 30000000 || b2 > 10 || num3 % 10000 != 0)
				{
					flag = false;
					break;
				}
				if (b2 >= 1)
				{
					flag2 = true;
				}
				array[i] = new GearSkillSubStat(num3, b2);
			}
			if (flag & flag2)
			{
				GearSkillSubStat[] array2 = array;
				for (int j = 0; j < array2.Length; j++)
				{
					GearSkillSubStat item = array2[j];
					if (item.Level == 0)
					{
						continue;
					}
					if (dictionary.TryGetValue(item.SkillId, out var value))
					{
						GearSkillSubStat gearSkillSubStat = list[value];
						int num4 = gearSkillSubStat.Level + item.Level;
						if (num4 <= 10)
						{
							list[value] = gearSkillSubStat with
							{
								Level = num4
							};
						}
					}
					else
					{
						dictionary[item.SkillId] = list.Count;
						list.Add(item);
					}
				}
				num += 1 + b * 5;
			}
			else
			{
				num++;
			}
		}
		return list;
	}

	private static IReadOnlyList<GearSubStat> FindSubStatBlockSkipping(ReadOnlySpan<byte> body, int searchStart, int searchEnd, int skillStart, int skillEnd)
	{
		searchEnd = Math.Min(searchEnd, body.Length);
		List<GearSubStat> list = new List<GearSubStat>(7);
		HashSet<int> hashSet = new HashSet<int>();
		int num = searchStart;
		while (num + 1 < searchEnd)
		{
			if (num >= skillStart && num < skillEnd)
			{
				num = skillEnd;
				continue;
			}
			byte b = body[num];
			if (b < 1 || b > 7 || num + 1 + b * 6 > body.Length || (num + 1 + b * 6 > skillStart && num < skillStart))
			{
				num++;
				continue;
			}
			bool flag = true;
			GearSubStat[] array = new GearSubStat[b];
			for (int i = 0; i < b; i++)
			{
				int num2 = num + 1 + i * 6;
				ushort num3 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num2, 2));
				ushort num4 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num2 + 2, 2));
				ushort num5 = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(num2 + 4, 2));
				if (num3 < 1 || num3 > 1000 || num4 < 1 || num4 > 50000 || num5 != 0)
				{
					flag = false;
					break;
				}
				array[i] = new GearSubStat(num3, num4);
			}
			if (flag)
			{
				GearSubStat[] array2 = array;
				for (int j = 0; j < array2.Length; j++)
				{
					GearSubStat item = array2[j];
					if (hashSet.Add(item.StatId))
					{
						list.Add(item);
					}
				}
				num += 1 + b * 6;
			}
			else
			{
				num++;
			}
		}
		return list;
	}
}
