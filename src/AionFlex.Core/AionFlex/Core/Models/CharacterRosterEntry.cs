namespace AionFlex.Core.Models;

public sealed record CharacterRosterEntry(int EntityId, string Name, int PcId, int Level, int GearScore, string? Legion, long LastOnlineUtcMs, int Slot, long CombatPower)
{
	public JobClass JobClass => PcClassMap.ToJobClass(PcId);

	public bool? IsAsmodian => PcClassMap.IsAsmodian(PcId);

	public bool IsPlaceholder
	{
		get
		{
			if (JobClass != JobClass.Unknown)
			{
				return Name.StartsWith('$');
			}
			return true;
		}
	}
}
