using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using AionFlex.Core.Models;
using AionFlex.Core.Validation;

namespace AionFlex.Core.Parsing;

public static class PartyRoomRosterDecoder
{
	public const byte Opcode0 = 2;

	public const byte Opcode1 = 151;

	public const int MaxRoomSize = 10;

	private const int MaxSlotEntries = 8;

	private const int SlotHeaderBytes = 11;

	private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	public static bool TryDecode(ReadOnlySpan<byte> record, out PartyRoomRoster? roster)
	{
		int recordLength;
		return TryDecode(record, out roster, out recordLength);
	}

	public static bool TryDecode(ReadOnlySpan<byte> record, out PartyRoomRoster? roster, out int recordLength)
	{
		roster = null;
		recordLength = 0;
		try
		{
			if (!TryDecodeCore(record, out roster, out var recordLength2))
			{
				return false;
			}
			recordLength = recordLength2;
			return true;
		}
		catch (DecoderFallbackException)
		{
			roster = null;
			return false;
		}
	}

	private static bool TryDecodeCore(ReadOnlySpan<byte> record, out PartyRoomRoster? roster, out int recordLength)
	{
		roster = null;
		recordLength = 0;
		var (num, num2) = ReadVarInt(record);
		if (num2 <= 0)
		{
			return false;
		}
		long num3 = (long)num2 + (long)num - 4;
		if (num3 < num2 + 2 || num3 > record.Length)
		{
			return false;
		}
		ReadOnlySpan<byte> s = record.Slice(0, (int)num3);
		int num4 = num2;
		if (s[num4] != 2 || s[num4 + 1] != 151)
		{
			return false;
		}
		num4 += 2;
		if (num4 + 5 > s.Length)
		{
			return false;
		}
		uint roomId = U32(s, num4);
		byte b = s[num4 + 4];
		num4 += 5;
		if (num4 + b > s.Length)
		{
			return false;
		}
		int start = num4;
		num4 += b;
		if (num4 + 5 + 2 + 8 + 4 > s.Length)
		{
			return false;
		}
		int num5 = s[num4];
		if ((num5 < 1 || num5 > 10) ? true : false)
		{
			return false;
		}
		int dungeonId = (int)U32(s, num4 + 1);
		num4 += 7;
		uint leaderCharacterId = U32(s, num4);
		if (U16(s, num4 + 4) != 0)
		{
			return false;
		}
		int leaderServerId = U16(s, num4 + 6);
		num4 += 8;
		if (s[num4 + 3] != num5)
		{
			return false;
		}
		num4 += 4;
		Span<byte> choice = stackalloc byte[num5];
		Span<byte> found = stackalloc byte[num5];
		int readings = 0;
		CountReadings(s, num4, 1, num5, choice, found, ref readings);
		if (readings != 1)
		{
			return false;
		}
		List<PartyRoomMember> list = new List<PartyRoomMember>(num5);
		for (int i = 1; i <= num5; i++)
		{
			int num6 = ReadSlot(s, num4, i, found[i - 1], out PartyRoomMember member);
			if ((object)member != null)
			{
				if (!PlayerNameValidator.IsPlausible(member.Name))
				{
					return false;
				}
				list.Add(member);
			}
			num4 = num6;
		}
		string title = StrictUtf8.GetString(s.Slice(start, b));
		roster = new PartyRoomRoster(roomId, title, num5, dungeonId, leaderCharacterId, leaderServerId, list);
		recordLength = s.Length;
		return true;
	}

	private static void CountReadings(ReadOnlySpan<byte> s, int o, int slot, int maxMembers, Span<byte> choice, Span<byte> found, ref int readings)
	{
		if (readings >= 2)
		{
			return;
		}
		if (slot > maxMembers)
		{
			if (s.Length - o == 2 && s[o] == 0)
			{
				if (readings == 0)
				{
					choice.CopyTo(found);
				}
				readings++;
			}
			return;
		}
		for (byte b = 0; b <= 1; b++)
		{
			int num = SlotEnd(s, o, slot, b);
			if (num >= 0)
			{
				choice[slot - 1] = b;
				CountReadings(s, num, slot + 1, maxMembers, choice, found, ref readings);
				if (readings >= 2)
				{
					break;
				}
			}
		}
	}

	private static int SlotEnd(ReadOnlySpan<byte> s, int o, int slot, int withState)
	{
		if (o + 11 > s.Length)
		{
			return -1;
		}
		byte b = s[o];
		if (s[o + 1] != slot)
		{
			return -1;
		}
		bool flag = b == 0;
		if (!flag && ((b & 0xC) != 12 || (b & -31) != 0))
		{
			return -1;
		}
		uint num = U32(s, o + 2);
		if (U16(s, o + 6) != 0)
		{
			return -1;
		}
		int num2 = U16(s, o + 8);
		int num3 = s[o + 10];
		int num4 = o + 11 + num3;
		if (num4 + 12 > s.Length)
		{
			return -1;
		}
		uint num5 = U32(s, num4);
		uint num6 = U32(s, num4 + 4);
		uint num7 = U32(s, num4 + 8);
		if (flag)
		{
			if (num != 0 || num2 != 0 || num3 != 0 || num5 != 0 || num6 != 0 || num7 != 0)
			{
				return -1;
			}
		}
		else
		{
			bool flag2 = num3 == 0 || !PcClassMap.IsKnownPcId((int)num5);
			if (!flag2)
			{
				bool flag3 = ((num6 < 1 || num6 > 99) ? true : false);
				flag2 = flag3;
			}
			if (flag2 || num7 > 99999)
			{
				return -1;
			}
		}
		int num8 = num4 + 12 + withState;
		if (!flag)
		{
			num8 += 4;
		}
		if (num8 + 10 > s.Length)
		{
			return -1;
		}
		int num9 = s[num8 + 9];
		if (num9 > 8)
		{
			return -1;
		}
		num8 += 10 + 5 * num9;
		if ((b & 0x10) != 0)
		{
			num8 += 8;
		}
		num8 += 2;
		if (num8 > s.Length)
		{
			return -1;
		}
		return num8;
	}

	private static int ReadSlot(ReadOnlySpan<byte> s, int o, int slot, int withState, out PartyRoomMember? member)
	{
		member = null;
		int result = SlotEnd(s, o, slot, withState);
		if (s[o] == 0)
		{
			return result;
		}
		uint characterId = U32(s, o + 2);
		int serverId = U16(s, o + 8);
		int num = s[o + 10];
		int num2 = o + 11;
		int num3 = num2 + num;
		string name = StrictUtf8.GetString(s.Slice(num2, num));
		int pcId = (int)U32(s, num3);
		int level = (int)U32(s, num3 + 4);
		int gearScore = (int)U32(s, num3 + 8);
		int num4 = num3 + 12 + withState;
		int worldServerId = U16(s, num4);
		int currentServerId = U16(s, num4 + 2);
		int combatPower = (int)U32(s, num4 + 5);
		member = new PartyRoomMember(slot, characterId, serverId, name, pcId, PcClassMap.ToJobClass(pcId), level, gearScore, combatPower, worldServerId, currentServerId);
		return result;
	}

	private static uint U32(ReadOnlySpan<byte> s, int o)
	{
		return BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(o, 4));
	}

	private static int U16(ReadOnlySpan<byte> s, int o)
	{
		return BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(o, 2));
	}

	private static (int Value, int Length) ReadVarInt(ReadOnlySpan<byte> bytes)
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 5; i++)
		{
			if (i >= bytes.Length)
			{
				return (Value: -1, Length: -1);
			}
			byte b = bytes[i];
			num |= (b & 0x7F) << num2;
			if ((b & 0x80) == 0)
			{
				if (num >= 0)
				{
					return (Value: num, Length: i + 1);
				}
				return (Value: -1, Length: -1);
			}
			num2 += 7;
		}
		return (Value: -1, Length: -1);
	}
}
