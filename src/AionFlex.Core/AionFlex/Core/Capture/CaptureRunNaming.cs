using System;
using System.Globalization;
using System.Text;

namespace AionFlex.Core.Capture;

public static class CaptureRunNaming
{
	public const int MaxPartLength = 60;

	public const string UnknownInstance = "Unknown";

	public static string BaseName(DateTime anchorLocal, string? instanceName, string? difficulty, int? dungeonId, int? serverId)
	{
		StringBuilder stringBuilder = new StringBuilder(anchorLocal.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture));
		stringBuilder.Append('_');
		string text = ((instanceName == null) ? "" : Sanitize(instanceName));
		if (text.Length == 0)
		{
			text = "Unknown";
			if (dungeonId.HasValue)
			{
				text = text + "-d" + dungeonId.GetValueOrDefault().ToString(CultureInfo.InvariantCulture);
			}
			if (serverId.HasValue)
			{
				text = text + "-s" + serverId.GetValueOrDefault().ToString(CultureInfo.InvariantCulture);
			}
		}
		stringBuilder.Append(text);
		string text2 = ((difficulty == null) ? "" : Sanitize(difficulty));
		if (text2.Length > 0 && !text.Equals(text2, StringComparison.OrdinalIgnoreCase) && !text.EndsWith("-" + text2, StringComparison.OrdinalIgnoreCase))
		{
			stringBuilder.Append('-').Append(text2);
		}
		return stringBuilder.ToString();
	}

	public static string Sanitize(string value)
	{
		StringBuilder stringBuilder = new StringBuilder(value.Length);
		bool flag = false;
		foreach (Rune item in value.EnumerateRunes())
		{
			if (IsApostrophe(item))
			{
				continue;
			}
			if (Rune.IsLetterOrDigit(item))
			{
				if (flag && stringBuilder.Length > 0)
				{
					stringBuilder.Append('-');
				}
				flag = false;
				stringBuilder.Append(item.ToString());
			}
			else
			{
				flag = true;
			}
		}
		string text = stringBuilder.ToString();
		if (text.Length <= 60)
		{
			return text;
		}
		if (text[60] == '-')
		{
			return text.Substring(0, 60);
		}
		int num = text.LastIndexOf('-', 59);
		if (num > 0)
		{
			return text.Substring(0, num);
		}
		int num2 = 60;
		if (char.IsHighSurrogate(text[num2 - 1]))
		{
			num2--;
		}
		return text.Substring(0, num2);
	}

	private static bool IsApostrophe(Rune rune)
	{
		int value = rune.Value;
		if (value == 39 || value == 700 || (uint)(value - 8216) <= 1u)
		{
			return true;
		}
		return false;
	}
}
