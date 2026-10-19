using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using AionFlex.Core.Models;

namespace AionFlex.Core.Parsing;

public static class InventoryRecordDecoder
{
	private ref struct Reader(ReadOnlySpan<byte> data, int offset)
	{
		private readonly ReadOnlySpan<byte> _data = data;

		private int _bits = 0;

		private int _pending = 0;

		public int Offset { get; private set; } = offset;

		public readonly bool PendingBitsSet
		{
			get
			{
				if (_pending > 0)
				{
					return _bits != 0;
				}
				return false;
			}
		}

		public bool Skip(int n)
		{
			if (n < 0 || Offset + n > _data.Length)
			{
				return false;
			}
			Offset += n;
			return true;
		}

		public bool U8(out byte value)
		{
			value = 0;
			if (Offset >= _data.Length)
			{
				return false;
			}
			value = _data[Offset++];
			return true;
		}

		public bool U32(out uint value)
		{
			value = 0u;
			if (Offset + 4 > _data.Length)
			{
				return false;
			}
			value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(Offset, 4));
			Offset += 4;
			return true;
		}

		public bool U64(out ulong value)
		{
			value = 0uL;
			if (Offset + 8 > _data.Length)
			{
				return false;
			}
			value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(Offset, 8));
			Offset += 8;
			return true;
		}

		public bool VarInt(out int value)
		{
			value = 0;
			for (int i = 0; i < 3; i++)
			{
				if (!U8(out var value2))
				{
					return false;
				}
				value |= (value2 & 0x7F) << 7 * i;
				if ((value2 & 0x80) == 0)
				{
					return true;
				}
			}
			return false;
		}

		public bool Bit()
		{
			if (_pending == 0)
			{
				if (!U8(out var value))
				{
					return false;
				}
				_bits = value;
				_pending = 8;
			}
			_bits >>= 1;
			_pending--;
			return true;
		}

		public bool List(int width)
		{
			if (!U8(out var value) || value > 32)
			{
				return false;
			}
			return Skip(value * width);
		}

		public bool Utf8(int length)
		{
			if (Offset + length > _data.Length)
			{
				return false;
			}
			if (length > 0)
			{
				try
				{
					Strict.GetString(_data.Slice(Offset, length));
				}
				catch (DecoderFallbackException)
				{
					return false;
				}
			}
			Offset += length;
			return true;
		}
	}

	public const byte ListOpcode = 17;

	public const byte ChangeOpcode = 27;

	public const byte OpcodeFamily = 86;

	public const uint BoundKinaItemId = 930100001u;

	public const uint TradableKinaItemId = 930100031u;

	public const uint AbyssPointsItemId = 930100003u;

	public const byte WalletContainer = 1;

	private const byte ListHeader0 = 16;

	private const byte ListHeader1 = 2;

	public const int ListTrailerBytes = 6;

	public const int ChangeEntryTailBytes = 3;

	private const int MaxListEntries = 4096;

	private const int MaxChangeEntries = 255;

	private const int MaxSubList = 32;

	private const int MaxCrafterBytes = 48;

	private const byte MaxOp = 2;

	private static readonly UTF8Encoding Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	public static bool TryDecodeList(ReadOnlySpan<byte> buffer, out IReadOnlyList<InventoryItem> items)
	{
		items = Array.Empty<InventoryItem>();
		if (!TryOpen(buffer, 17, out var offset, out var end))
		{
			return false;
		}
		if (offset + 2 > end || buffer[offset] != 16 || buffer[offset + 1] != 2)
		{
			return false;
		}
		Reader r = new Reader(buffer.Slice(0, end), offset + 2);
		if (!r.VarInt(out var value) || value < 1 || value > 4096)
		{
			return false;
		}
		List<InventoryItem> list = new List<InventoryItem>(value);
		for (int i = 0; i < value; i++)
		{
			if (!TryReadEntry(ref r, out var item))
			{
				return false;
			}
			list.Add(item);
		}
		if (r.PendingBitsSet || r.Offset + 6 != end)
		{
			return false;
		}
		items = list;
		return true;
	}

	public static bool TryDecodeChange(ReadOnlySpan<byte> buffer, out IReadOnlyList<InventoryItemChange> changes)
	{
		changes = Array.Empty<InventoryItemChange>();
		if (!TryOpen(buffer, 27, out var offset, out var end))
		{
			return false;
		}
		Reader r = new Reader(buffer.Slice(0, end), offset);
		if (!r.U8(out var value) || value < 1 || value > byte.MaxValue)
		{
			return false;
		}
		List<InventoryItemChange> list = new List<InventoryItemChange>(value);
		for (int i = 0; i < value; i++)
		{
			if (!r.U8(out var value2) || value2 > 2)
			{
				return false;
			}
			if (!TryReadEntry(ref r, out var item))
			{
				return false;
			}
			if (!r.Skip(3))
			{
				return false;
			}
			list.Add(new InventoryItemChange((InventoryItemOp)value2, item));
		}
		if (r.PendingBitsSet || r.Offset != end)
		{
			return false;
		}
		changes = list;
		return true;
	}

	public static bool TryFindListInBundle(ReadOnlySpan<byte> data, out IReadOnlyList<InventoryItem> items)
	{
		items = Array.Empty<InventoryItem>();
		bool result = false;
		for (int i = 2; i + 3 < data.Length; i++)
		{
			if (data[i] != 17 || data[i + 1] != 86 || data[i + 2] != 16 || data[i + 3] != 2)
			{
				continue;
			}
			for (int num = 3; num >= 2; num--)
			{
				int num2 = i - num;
				if (num2 >= 0)
				{
					int num3 = num2;
					if (TryDecodeList(data.Slice(num3, data.Length - num3), out IReadOnlyList<InventoryItem> items2))
					{
						items = items2;
						result = true;
						break;
					}
				}
			}
		}
		return result;
	}

	public static IReadOnlyList<WalletItem>? WalletOf(IReadOnlyList<InventoryItem> items)
	{
		List<WalletItem> list = new List<WalletItem>(3);
		foreach (InventoryItem item in items)
		{
			if (item.Container == 1 && TryCurrency(item.ItemId, out var currency))
			{
				if (list.Exists((WalletItem w) => w.Currency == currency))
				{
					return null;
				}
				list.Add(new WalletItem(currency, item.Handle, InventoryItemOp.Changed, ToCount(item.Count)));
			}
		}
		return list;
	}

	public static IReadOnlyList<WalletItem> WalletOf(IReadOnlyList<InventoryItemChange> changes)
	{
		List<WalletItem> list = new List<WalletItem>();
		foreach (InventoryItemChange change in changes)
		{
			if (change.Item.Container == 1 && TryCurrency(change.Item.ItemId, out var currency))
			{
				list.Add(new WalletItem(currency, change.Item.Handle, change.Op, ToCount(change.Item.Count)));
			}
		}
		return list;
	}

	public static bool TryCurrency(uint itemId, out WalletCurrency currency)
	{
		switch (itemId)
		{
		case 930100001u:
			currency = WalletCurrency.BoundKina;
			return true;
		case 930100031u:
			currency = WalletCurrency.TradableKina;
			return true;
		case 930100003u:
			currency = WalletCurrency.AbyssPoints;
			return true;
		default:
			currency = WalletCurrency.BoundKina;
			return false;
		}
	}

	private static long ToCount(ulong count)
	{
		if (count <= long.MaxValue)
		{
			return (long)count;
		}
		return long.MaxValue;
	}

	private static bool TryReadEntry(ref Reader r, out InventoryItem item)
	{
		item = default;
		if (!r.U8(out var value) || value > 1)
		{
			return false;
		}
		if (!r.U64(out var value2) || !r.U32(out var value3) || !r.U64(out var value4) || !r.U8(out var value5) || !r.U8(out var value6))
		{
			return false;
		}
		if (!r.Skip(3) || !r.Bit())
		{
			return false;
		}
		if (!r.U8(out var value7) || value7 > 48 || !r.Utf8(value7))
		{
			return false;
		}
		if (!r.Skip(8) || !r.Bit())
		{
			return false;
		}
		if (value == 1 && !TryReadDetails(ref r))
		{
			return false;
		}
		item = new InventoryItem(value2, value3, value4, value5, value6);
		return true;
	}

	private static bool TryReadDetails(ref Reader r)
	{
		if (!r.Skip(18) || !r.Bit() || !r.Skip(1))
		{
			return false;
		}
		if (!r.List(7))
		{
			return false;
		}
		if (!r.Skip(9))
		{
			return false;
		}
		if (!r.List(4))
		{
			return false;
		}
		if (!r.List(6) || !r.List(6) || !r.List(6))
		{
			return false;
		}
		if (!r.List(5) || !r.List(5))
		{
			return false;
		}
		if (!r.List(6) || !r.List(5))
		{
			return false;
		}
		return r.Skip(1);
	}

	public static int RecordLength(ReadOnlySpan<byte> buffer)
	{
		if (!TryOpen(buffer, 17, out var offset, out var end) && !TryOpen(buffer, 27, out offset, out end))
		{
			return 0;
		}
		return end;
	}

	private static bool TryOpen(ReadOnlySpan<byte> buffer, byte opcode, out int offset, out int end)
	{
		offset = 0;
		end = 0;
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 4; i++)
		{
			if (i >= buffer.Length)
			{
				return false;
			}
			byte b = buffer[i];
			num |= (b & 0x7F) << 7 * i;
			if ((b & 0x80) == 0)
			{
				num2 = i + 1;
				break;
			}
		}
		if (num2 == 0)
		{
			return false;
		}
		end = num2 + num - 4;
		if (end < num2 + 2 || end > buffer.Length)
		{
			return false;
		}
		if (buffer[num2] != opcode || buffer[num2 + 1] != 86)
		{
			return false;
		}
		offset = num2 + 2;
		return true;
	}
}
