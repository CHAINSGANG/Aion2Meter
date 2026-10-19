namespace AionFlex.Core.Models;

public sealed record EquippedTitlesSnapshot(int Slot1TitleId, int Slot2TitleId, int Slot3TitleId)
{
	public bool IsEmpty
	{
		get
		{
			if (Slot1TitleId == 0 && Slot2TitleId == 0)
			{
				return Slot3TitleId == 0;
			}
			return false;
		}
	}

	public static readonly EquippedTitlesSnapshot Empty = new EquippedTitlesSnapshot(0, 0, 0);
}
