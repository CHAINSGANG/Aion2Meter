namespace AionFlex.Core.Models;

public static class SkillCodeNormalizer
{
	public static int Normalize(int skillCode)
	{
		if (skillCode >= 11000000 && skillCode <= 19999999)
		{
			return skillCode / 10000 * 10000;
		}
		return skillCode;
	}
}
