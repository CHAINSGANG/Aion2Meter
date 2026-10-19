using System.Collections.Generic;

namespace AionFlex.Core.Servers;

public static class ServerIdSpace
{
	public const int GlobalBlockSize = 60;

	public static IReadOnlyList<int> GlobalBlocks { get; } = new int[10] { 11, 12, 13, 14, 15, 21, 22, 23, 24, 25 };

	public static int GlobalBlockSlots(int block)
	{
		if (((uint)(block - 11) > 4u && (uint)(block - 21) > 4u) || 1 == 0)
		{
			return 0;
		}
		return 60;
	}

	public static bool IsTwKrServerId(int id)
	{
		if (id >= 2001)
		{
			if (id <= 2058)
			{
				goto IL_0022;
			}
		}
		else if (id >= 1001 && id <= 1058)
		{
			goto IL_0022;
		}
		return false;
		IL_0022:
		return true;
	}

	public static bool IsGlobalServerId(int id)
	{
		if (id <= 0)
		{
			return false;
		}
		int num = id % 100;
		if (num >= 1)
		{
			return num <= GlobalBlockSlots(id / 100);
		}
		return false;
	}

	public static bool IsKnownServerId(int id)
	{
		if (!IsTwKrServerId(id))
		{
			return IsGlobalServerId(id);
		}
		return true;
	}

	public static string? FactionOf(int id)
	{
		if (!IsKnownServerId(id))
		{
			return null;
		}
		if (id >= 2000)
		{
			return "Asmodian";
		}
		return "Elyos";
	}

	public static string? FactionOf(int? id)
	{
		if (id.HasValue)
		{
			int valueOrDefault = id.GetValueOrDefault();
			return FactionOf(valueOrDefault);
		}
		return null;
	}
}
