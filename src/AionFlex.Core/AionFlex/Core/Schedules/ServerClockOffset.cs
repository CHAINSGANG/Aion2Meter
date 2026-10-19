using System;

namespace AionFlex.Core.Schedules;

public static class ServerClockOffset
{
	public const long StepMs = 900000L;

	public const long MaxAbsMs = 50400000L;

	private const ulong MinEpochMs = 1577836800000uL;

	private const ulong MaxEpochMs = 4102444800000uL;

	public static ReadOnlySpan<byte> Header1536 => new byte[4] { 21, 54, 0, 0 };

	public static bool IsPlausible(long offsetMs)
	{
		if (offsetMs % 900000 == 0L)
		{
			return Math.Abs(offsetMs) <= 50400000;
		}
		return false;
	}

	public static bool TryReadFrom00E2(ReadOnlySpan<byte> record, out TimeSpan offset)
	{
		offset = default;
		if (record.Length < 12 || record[0] != 18 || record[1] != 0 || record[2] != 226 || record[3] != 0)
		{
			return false;
		}
		long num = BitConverter.ToInt64(record.Slice(4, 8));
		if (num == long.MinValue || !IsPlausible(-num))
		{
			return false;
		}
		offset = TimeSpan.FromMilliseconds(-num);
		return true;
	}

	public static bool TryReadFrom1536(ReadOnlySpan<byte> record, out TimeSpan offset)
	{
		offset = default;
		if (TryHeader1536(record, out var at))
		{
			return TryReadStamps(record, at, out offset);
		}
		return false;
	}

	public static bool TryReadAt(ReadOnlySpan<byte> data, int opcodeAt, out TimeSpan offset, out int? namedServerId)
	{
		offset = default;
		namedServerId = null;
		if (opcodeAt < 0 || opcodeAt + 4 > data.Length || !data.Slice(opcodeAt, 4).SequenceEqual(Header1536))
		{
			return false;
		}
		if (!TryReadStamps(data, opcodeAt + 4, out offset))
		{
			return false;
		}
		if (TryReadServerIdAfterStamps(data, opcodeAt + 4, out var serverId))
		{
			namedServerId = serverId;
		}
		return true;
	}

	private static bool TryReadStamps(ReadOnlySpan<byte> record, int at, out TimeSpan offset)
	{
		offset = default;
		if (at + 16 > record.Length)
		{
			return false;
		}
		ulong num = BitConverter.ToUInt64(record.Slice(at, 8));
		ulong num2 = BitConverter.ToUInt64(record.Slice(at + 8, 8));
		bool flag = ((num < 1577836800000L || num > 4102444800000L) ? true : false);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = ((num2 < 1577836800000L || num2 > 4102444800000L) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return false;
		}
		long num3 = (long)(num2 - num);
		if (!IsPlausible(num3))
		{
			return false;
		}
		offset = TimeSpan.FromMilliseconds(num3);
		return true;
	}

	public static bool TryReadServerIdFrom1536(ReadOnlySpan<byte> record, out int serverId)
	{
		serverId = 0;
		if (TryHeader1536(record, out var at))
		{
			return TryReadServerIdAfterStamps(record, at, out serverId);
		}
		return false;
	}

	private static bool TryReadServerIdAfterStamps(ReadOnlySpan<byte> record, int at, out int serverId)
	{
		serverId = 0;
		int num = at + 16 + 2;
		if (num + 2 > record.Length || record[num] != 4)
		{
			return false;
		}
		byte b = record[num + 1];
		if ((b > 80 || b == 0) ? true : false)
		{
			return false;
		}
		num += 2 + b;
		if (num + 5 > record.Length || record[num] != 4)
		{
			return false;
		}
		int num2 = 0;
		for (int i = 1; i <= 4; i++)
		{
			byte b2 = record[num + i];
			if ((b2 < 48 || b2 > 57) ? true : false)
			{
				return false;
			}
			num2 = num2 * 10 + (b2 - 48);
		}
		serverId = num2;
		return true;
	}

	private static bool TryHeader1536(ReadOnlySpan<byte> record, out int at)
	{
		at = 0;
		int i;
		for (i = 0; i < record.Length && i < 3 && (record[i] & 0x80) != 0; i++)
		{
		}
		i++;
		if (i + 4 + 16 > record.Length)
		{
			return false;
		}
		if (record[i] != 21 || record[i + 1] != 54 || record[i + 2] != 0 || record[i + 3] != 0)
		{
			return false;
		}
		at = i + 4;
		return true;
	}
}
