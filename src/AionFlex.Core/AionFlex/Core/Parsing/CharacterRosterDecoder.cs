using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using AionFlex.Core.Models;
using AionFlex.Core.Schedules;

namespace AionFlex.Core.Parsing;

public static class CharacterRosterDecoder
{
	private readonly record struct Head(int EntityId, string Name, int PcId, int Level, int GearScore, string? Legion, long LastOnlineMs);

	public const byte OpcodeFamily = 54;

	public const byte ChangeCharacterOpcode = 38;

	public const byte CharacterSelectOpcode = 21;

	public const int MaxCharacters = 64;

	private const byte ListEnd = 4;

	private const int NameMaxBytes = 48;

	private const int LegionMaxBytes = 48;

	private const int MaxLevel = 60;

	private const long MaxCombatPower = 20000000L;

	private const long MinUnixMs = 1600000000000L;

	private const long MaxUnixMs = 2100000000000L;

	private const int TailBytes = 9;

	private const int MinTrailerTailBytes = 1;

	private const int MaxTrailerTailBytes = 8;

	public static bool TryDecode(ReadOnlySpan<byte> buffer, out CharacterRoster? roster)
	{
		roster = null;
		try
		{
			return TryDecodeCore(buffer, out roster);
		}
		catch (Exception)
		{
			roster = null;
			return false;
		}
	}

	private static bool TryDecodeCore(ReadOnlySpan<byte> buffer, out CharacterRoster? roster)
	{
		roster = null;
		if (!TryReadVarInt(buffer, 0, out var value, out var length) || length > 3)
		{
			return false;
		}
		long num = (long)length + (long)value - 4;
		if (num > buffer.Length || num < length + 6)
		{
			return false;
		}
		ReadOnlySpan<byte> r = buffer.Slice(0, (int)num);
		int num2 = length;
		if (r[num2 + 1] != 54)
		{
			return false;
		}
		CharacterRosterKind characterRosterKind;
		if (r[num2] == 38)
		{
			characterRosterKind = CharacterRosterKind.ChangeCharacter;
		}
		else
		{
			if (r[num2] != 21)
			{
				return false;
			}
			characterRosterKind = CharacterRosterKind.CharacterSelect;
		}
		num2 += 2;
		if (r[num2] != 0 || r[num2 + 1] != 0)
		{
			return false;
		}
		num2 += 2;
		if (characterRosterKind == CharacterRosterKind.CharacterSelect)
		{
			if (num2 + 16 > r.Length)
			{
				return false;
			}
			long num3 = BinaryPrimitives.ReadInt64LittleEndian(r.Slice(num2, 8));
			long num4 = BinaryPrimitives.ReadInt64LittleEndian(r.Slice(num2 + 8, 8));
			bool flag = ((num3 < 1600000000000L || num3 > 2100000000000L) ? true : false);
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3 = ((num4 < 1600000000000L || num4 > 2100000000000L) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				return false;
			}
			if (!ServerClockOffset.IsPlausible(num4 - num3))
			{
				return false;
			}
			num2 += 16;
		}
		if (num2 >= r.Length)
		{
			return false;
		}
		int num5 = r[num2++];
		if (num5 < 1 || num5 > 64)
		{
			return false;
		}
		List<(int, int, Head)> list = new List<(int, int, Head)>(num5);
		if (!TryReadHead(r, num2, out var head, out var fixedEnd))
		{
			return false;
		}
		list.Add((num2, fixedEnd, head));
		while (list.Count < num5)
		{
			int num6 = list[list.Count - 1].Item2 + 9;
			bool flag4 = false;
			for (int i = num6; i < r.Length; i++)
			{
				if (TryReadHead(r, i, out var head2, out var fixedEnd2))
				{
					list.Add((i, fixedEnd2, head2));
					flag4 = true;
					break;
				}
			}
			if (!flag4)
			{
				return false;
			}
		}
		int? serverId = null;
		int item = list[list.Count - 1].Item2;
		int num7;
		if (characterRosterKind == CharacterRosterKind.ChangeCharacter)
		{
			if (r[r.Length - 1] != 4)
			{
				return false;
			}
			num7 = r.Length - 1;
			if (num7 - 9 < item)
			{
				return false;
			}
		}
		else
		{
			num7 = -1;
			for (int j = item + 9; j < r.Length; j++)
			{
				if (r[j] == 4 && TryReadTrailer(r, j + 1, out var serverId2))
				{
					num7 = j;
					serverId = serverId2;
					break;
				}
			}
			if (num7 < 0)
			{
				return false;
			}
		}
		List<CharacterRosterEntry> list2 = new List<CharacterRosterEntry>(num5);
		HashSet<int> hashSet = new HashSet<int>();
		for (int k = 0; k < num5; k++)
		{
			int num8 = ((k + 1 < num5) ? list[k + 1].Item1 : num7) - 9;
			if (num8 < list[k].Item2)
			{
				return false;
			}
			int num9 = r[num8];
			long num10 = BinaryPrimitives.ReadInt64LittleEndian(r.Slice(num8 + 1, 8));
			if (num9 >= num5 || !hashSet.Add(num9))
			{
				return false;
			}
			if (num10 < 1 || num10 > 20000000)
			{
				return false;
			}
			Head item2 = list[k].Item3;
			list2.Add(new CharacterRosterEntry(item2.EntityId, item2.Name, item2.PcId, item2.Level, item2.GearScore, item2.Legion, item2.LastOnlineMs, num9, num10));
		}
		roster = new CharacterRoster(characterRosterKind, list2, serverId);
		return true;
	}

	private static bool TryReadHead(ReadOnlySpan<byte> r, int at, out Head head, out int fixedEnd)
	{
		head = default;
		fixedEnd = 0;
		int num = at;
		if (num + 5 > r.Length)
		{
			return false;
		}
		uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(num, 4));
		if (num2 == 0 || num2 > int.MaxValue)
		{
			return false;
		}
		num += 4;
		int num3 = r[num++];
		if (num3 < 1 || num3 > 48 || num + num3 > r.Length)
		{
			return false;
		}
		if (!TryReadText(r.Slice(num, num3), isName: true, out string text))
		{
			return false;
		}
		num += num3;
		if (num + 4 + 2 + 1 + 7 + 4 + 8 + 1 + 11 + 1 > r.Length)
		{
			return false;
		}
		uint num4 = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(num, 4));
		if (num4 < 1 || num4 > 48)
		{
			return false;
		}
		num += 4;
		if (r[num] != 1 || r[num + 1] != 2)
		{
			return false;
		}
		num += 2;
		int num5 = r[num++];
		if (num5 < 1 || num5 > 60)
		{
			return false;
		}
		if (r.Slice(num, 7).IndexOfAnyExcept((byte)0) >= 0)
		{
			return false;
		}
		num += 7;
		uint num6 = BinaryPrimitives.ReadUInt32LittleEndian(r.Slice(num, 4));
		if (num6 > int.MaxValue)
		{
			return false;
		}
		num += 12;
		if (r[num++] != 1)
		{
			return false;
		}
		if (r.Slice(num, 11).IndexOfAnyExcept((byte)0) >= 0)
		{
			return false;
		}
		num += 11;
		int num7 = r[num++];
		if (num7 > 48 || num + num7 > r.Length)
		{
			return false;
		}
		string legion = null;
		if (num7 > 0)
		{
			if (!TryReadText(r.Slice(num, num7), isName: false, out string text2))
			{
				return false;
			}
			legion = text2;
		}
		num += num7;
		if (num + 8 + 12 + 4 + 8 > r.Length)
		{
			return false;
		}
		long num8 = BinaryPrimitives.ReadInt64LittleEndian(r.Slice(num, 8));
		if ((num8 < 1600000000000L || num8 > 2100000000000L) ? true : false)
		{
			return false;
		}
		num += 24;
		long num9 = BinaryPrimitives.ReadInt64LittleEndian(r.Slice(num, 8));
		if ((num9 < 1600000000000L || num9 > 2100000000000L) ? true : false)
		{
			return false;
		}
		num += 8;
		head = new Head((int)num2, text, (int)num4, num5, (int)num6, legion, num8);
		fixedEnd = num;
		return true;
	}

	private static bool TryReadTrailer(ReadOnlySpan<byte> r, int at, out int? serverId)
	{
		serverId = null;
		int num = at;
		for (int i = 0; i < 3; i++)
		{
			if (num >= r.Length)
			{
				return false;
			}
			int num2 = r[num++];
			if (num2 < 1 || num2 > 64 || num + num2 > r.Length)
			{
				return false;
			}
			ReadOnlySpan<byte> readOnlySpan = r.Slice(num, num2);
			ReadOnlySpan<byte> readOnlySpan2 = readOnlySpan;
			for (int j = 0; j < readOnlySpan2.Length; j++)
			{
				byte b = readOnlySpan2[j];
				if ((b < 32 || b > 126) ? true : false)
				{
					return false;
				}
			}
			if (i == 1 && num2 == 4)
			{
				int num3 = 0;
				bool flag = true;
				readOnlySpan2 = readOnlySpan;
				for (int j = 0; j < readOnlySpan2.Length; j++)
				{
					byte b2 = readOnlySpan2[j];
					if ((b2 < 48 || b2 > 57) ? true : false)
					{
						flag = false;
						break;
					}
					num3 = num3 * 10 + (b2 - 48);
				}
				if (flag)
				{
					serverId = num3;
				}
			}
			num += num2;
		}
		int num4 = r.Length - num;
		if (num4 >= 1)
		{
			return num4 <= 8;
		}
		return false;
	}

	private static bool TryReadText(ReadOnlySpan<byte> bytes, bool isName, out string text)
	{
		text = "";
		int i = 0;
		StringBuilder stringBuilder = new StringBuilder(bytes.Length);
		int bytesConsumed;
		for (; i < bytes.Length; i += bytesConsumed)
		{
			int num = i;
			if (Rune.DecodeFromUtf8(bytes.Slice(num, bytes.Length - num), out var result, out bytesConsumed) != OperationStatus.Done)
			{
				return false;
			}
			if (isName)
			{
				if (!Rune.IsLetterOrDigit(result) && result.Value != 36)
				{
					return false;
				}
			}
			else if (Rune.IsControl(result))
			{
				return false;
			}
			stringBuilder.Append(result.ToString());
		}
		text = stringBuilder.ToString();
		return true;
	}

	private static bool TryReadVarInt(ReadOnlySpan<byte> buffer, int offset, out int value, out int length)
	{
		value = 0;
		length = 0;
		int num = 0;
		for (int i = 0; i < 5; i++)
		{
			if (offset + i >= buffer.Length)
			{
				return false;
			}
			byte b = buffer[offset + i];
			value |= (b & 0x7F) << num;
			if ((b & 0x80) == 0)
			{
				length = i + 1;
				return value >= 0;
			}
			num += 7;
		}
		return false;
	}
}
