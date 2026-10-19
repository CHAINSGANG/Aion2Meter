using AionFlex.Core.Models;

namespace AionFlex.Core.Armory;

public sealed record ArmoryCharacter(string CharacterId, int ServerId, string ServerName, string Name, int Level, int PcId)
{
	public JobClass JobClass
	{
		get
		{
			if (!PcClassMap.IsKnownPcId(PcId))
			{
				return JobClass.Unknown;
			}
			return PcClassMap.ToJobClass(PcId);
		}
	}
}
