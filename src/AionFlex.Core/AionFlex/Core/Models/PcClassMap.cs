namespace AionFlex.Core.Models;

public static class PcClassMap
{
	private const int BlockSize = 4;

	private static readonly JobClass[] Blocks = new JobClass[12]
	{
		JobClass.Unknown,
		JobClass.Gladiator,
		JobClass.Templar,
		JobClass.Ranger,
		JobClass.Assassin,
		JobClass.Elementalist,
		JobClass.Sorcerer,
		JobClass.Cleric,
		JobClass.Chanter,
		JobClass.Unknown,
		JobClass.Unknown,
		JobClass.Fighter
	};

	public const int MaxPcId = 48;

	public static bool IsKnownPcId(int pcId)
	{
		if (pcId >= 1)
		{
			return pcId <= 48;
		}
		return false;
	}

	public static JobClass ToJobClass(int pcId)
	{
		if (!IsKnownPcId(pcId))
		{
			return JobClass.Unknown;
		}
		return Blocks[(pcId - 1) / 4];
	}

	public static bool? IsAsmodian(int pcId)
	{
		if (!IsKnownPcId(pcId))
		{
			return null;
		}
		return (pcId - 1) % 4 >= 2;
	}

	public static bool? IsFemale(int pcId)
	{
		if (!IsKnownPcId(pcId))
		{
			return null;
		}
		return (pcId - 1) % 2 == 1;
	}
}
