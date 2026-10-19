namespace AionFlex.Core.Combat;

public readonly record struct ContributionCheck(bool LineApplies, ContributionDoubt Doubt = ContributionDoubt.None)
{
	public bool Reliable => Doubt == ContributionDoubt.None;
}
