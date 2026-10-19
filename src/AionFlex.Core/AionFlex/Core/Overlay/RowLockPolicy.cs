namespace AionFlex.Core.Overlay;

public static class RowLockPolicy
{
	public static bool IsLocked(bool? notEntitled, bool? isCurrentUser, bool ownMode)
	{
		bool flag = notEntitled ?? true;
		bool flag2 = isCurrentUser ?? ownMode;
		if (!ownMode)
		{
			if (flag)
			{
				return !flag2;
			}
			return false;
		}
		return flag & flag2;
	}
}
