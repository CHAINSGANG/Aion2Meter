using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class LootEnergyRecordDecoder
{
	public const uint LootEnergyTicketId = 60000001u;

	public const byte ListOpcode = 11;

	public const byte UpdateOpcode = 12;

	public const byte OpcodeFamily = 97;

	private const byte FlagU64 = 1;

	private const byte FlagBasic = 4;

	private const byte FlagBonus = 8;

	private const byte KnownFlags = 13;

	private const int MaxEntries = 1000;

	public static bool TryDecodeList(ReadOnlySpan<byte> buffer, out LootEnergyReading reading)
	{
		reading = default;
		if (!TryOpen(buffer, 11, out var offset, out var end))
		{
			return false;
		}
		if (!TryReadVarInt(buffer, ref offset, end, out var value) || value < 1 || value > 1000)
		{
			return false;
		}
		LootEnergyReading? lootEnergyReading = null;
		for (int i = 0; i < value; i++)
		{
			if (!TryReadEntry(buffer, ref offset, end, out var id, out var entry))
			{
				return false;
			}
			if (id == 60000001)
			{
				lootEnergyReading = entry;
			}
		}
		if (offset != end || !lootEnergyReading.HasValue)
		{
			return false;
		}
		reading = lootEnergyReading.Value;
		return true;
	}

	public static bool TryDecodeUpdate(ReadOnlySpan<byte> buffer, out LootEnergyReading reading)
	{
		reading = default;
		if (!TryOpen(buffer, 12, out var offset, out var end))
		{
			return false;
		}
		if (offset >= end)
		{
			return false;
		}
		byte b = buffer[offset++];
		if (b > 1)
		{
			return false;
		}
		if (!TryReadEntry(buffer, ref offset, end, out var id, out var entry))
		{
			return false;
		}
		offset += 1 + ((b == 1) ? 4 : 0);
		if (offset != end || id != 60000001)
		{
			return false;
		}
		reading = entry;
		return true;
	}

	public static bool TryDecodeListEntries(ReadOnlySpan<byte> buffer, out IReadOnlyList<ContentsTicketEntry> entries)
	{
		entries = Array.Empty<ContentsTicketEntry>();
		if (!TryOpen(buffer, 11, out var offset, out var end))
		{
			return false;
		}
		if (!TryReadVarInt(buffer, ref offset, end, out var value) || value < 1 || value > 1000)
		{
			return false;
		}
		List<ContentsTicketEntry> list = new List<ContentsTicketEntry>(value);
		for (int i = 0; i < value; i++)
		{
			if (!TryReadFullEntry(buffer, ref offset, end, out var entry))
			{
				return false;
			}
			list.Add(entry);
		}
		if (offset != end || !list.Exists((ContentsTicketEntry e) => e.TicketId == 60000001))
		{
			return false;
		}
		entries = list;
		return true;
	}

	public static bool TryDecodeUpdateEntry(ReadOnlySpan<byte> buffer, out ContentsTicketEntry entry)
	{
		entry = default;
		if (!TryOpen(buffer, 12, out var offset, out var end))
		{
			return false;
		}
		if (offset >= end)
		{
			return false;
		}
		byte b = buffer[offset++];
		if (b > 1)
		{
			return false;
		}
		if (!TryReadFullEntry(buffer, ref offset, end, out entry))
		{
			return false;
		}
		offset += 1 + ((b == 1) ? 4 : 0);
		return offset == end;
	}

	public static bool TryFindListEntriesInBundle(ReadOnlySpan<byte> data, out IReadOnlyList<ContentsTicketEntry> entries)
	{
		entries = Array.Empty<ContentsTicketEntry>();
		bool result = false;
		for (int i = 1; i + 1 < data.Length; i++)
		{
			if (data[i] != 11 || data[i + 1] != 97)
			{
				continue;
			}
			for (int num = 2; num >= 1; num--)
			{
				int num2 = i - num;
				if (num2 >= 0)
				{
					int num3 = num2;
					if (TryDecodeListEntries(data.Slice(num3, data.Length - num3), out IReadOnlyList<ContentsTicketEntry> entries2))
					{
						entries = entries2;
						result = true;
						break;
					}
				}
			}
		}
		return result;
	}

	private static bool TryReadFullEntry(ReadOnlySpan<byte> buffer, ref int offset, int end, out ContentsTicketEntry entry)
	{
		entry = default;
		if (offset + 5 > end)
		{
			return false;
		}
		byte b = buffer[offset];
		if ((b & -14) != 0)
		{
			return false;
		}
		uint ticketId = BitConverter.ToUInt32(buffer.Slice(offset + 1, 4));
		offset += 5;
		ulong? raw = null;
		if ((b & 1) != 0)
		{
			if (offset + 8 > end)
			{
				return false;
			}
			raw = BitConverter.ToUInt64(buffer.Slice(offset, 8));
			offset += 8;
		}
		int value = 0;
		int value2 = 0;
		if ((b & 4) != 0 && !TryReadVarInt(buffer, ref offset, end, out value))
		{
			return false;
		}
		if ((b & 8) != 0 && !TryReadVarInt(buffer, ref offset, end, out value2))
		{
			return false;
		}
		entry = new ContentsTicketEntry(ticketId, b, raw, value, value2);
		return true;
	}

	public static bool TryFindListInBundle(ReadOnlySpan<byte> data, out LootEnergyReading reading)
	{
		reading = default;
		bool result = false;
		for (int i = 1; i + 1 < data.Length; i++)
		{
			if (data[i] != 11 || data[i + 1] != 97)
			{
				continue;
			}
			for (int num = 2; num >= 1; num--)
			{
				int num2 = i - num;
				if (num2 >= 0)
				{
					int num3 = num2;
					if (TryDecodeList(data.Slice(num3, data.Length - num3), out var reading2))
					{
						reading = reading2;
						result = true;
						break;
					}
				}
			}
		}
		return result;
	}

	private static bool TryOpen(ReadOnlySpan<byte> buffer, byte opcode, out int offset, out int end)
	{
		offset = 0;
		end = 0;
		int offset2 = 0;
		if (!TryReadVarInt(buffer, ref offset2, buffer.Length, out var value))
		{
			return false;
		}
		int num = offset2;
		end = num + value - 4;
		if (end < num + 2 || end > buffer.Length)
		{
			return false;
		}
		if (buffer[num] != opcode || buffer[num + 1] != 97)
		{
			return false;
		}
		offset = num + 2;
		return true;
	}

	private static bool TryReadEntry(ReadOnlySpan<byte> buffer, ref int offset, int end, out uint id, out LootEnergyReading entry)
	{
		id = 0u;
		entry = default;
		if (offset + 5 > end)
		{
			return false;
		}
		byte b = buffer[offset];
		if ((b & -14) != 0)
		{
			return false;
		}
		id = BitConverter.ToUInt32(buffer.Slice(offset + 1, 4));
		offset += 5;
		if ((b & 1) != 0)
		{
			if (offset + 8 > end)
			{
				return false;
			}
			offset += 8;
		}
		int value = 0;
		int value2 = 0;
		if ((b & 4) != 0 && !TryReadVarInt(buffer, ref offset, end, out value))
		{
			return false;
		}
		if ((b & 8) != 0 && !TryReadVarInt(buffer, ref offset, end, out value2))
		{
			return false;
		}
		entry = new LootEnergyReading(value, value2);
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
