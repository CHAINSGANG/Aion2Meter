using System.Collections.Generic;

namespace AionFlex.Core.Validation;

public static class PlayerNameValidator
{
	public const int MaxLength = 24;

	private static readonly HashSet<char> ForbiddenChars = new HashSet<char>
	{
		'"', '\'', '`', ':', ',', ';', '{', '}', '[', ']',
		'(', ')', '<', '>', '\\', '/', '=', '|', '&', '#',
		'$', '%', '*', '@', '^', '~', '+', '\ufffd'
	};

	public static bool IsPlausible(string? name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}
		string text = name.Trim();
		if (text.Length == 0 || text.Length > 24)
		{
			return false;
		}
		int num = 0;
		string text2 = text;
		foreach (char c in text2)
		{
			if (char.IsControl(c))
			{
				return false;
			}
			if (ForbiddenChars.Contains(c))
			{
				return false;
			}
			if (char.IsLetter(c))
			{
				num++;
			}
		}
		if (num == 0)
		{
			return false;
		}
		return true;
	}
}
