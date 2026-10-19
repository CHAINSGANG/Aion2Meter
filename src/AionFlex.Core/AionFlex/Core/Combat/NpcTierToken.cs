using System;
using System.Text.RegularExpressions;

namespace AionFlex.Core.Combat;

public static class NpcTierToken
{
	public const string Base = "Base";

	public const string BareGrade = "Grade";

	public const string Easy = "Easy";

	public const string Hard = "Hard";

	private static readonly Regex AwakenTierRe = new Regex("^M_Dungeon_Awaken_\\d{3}_(\\d{2})_", RegexOptions.Compiled);

	private static readonly Regex FrozenLamentTierRe = new Regex("M_PD_FrozenLament_0(\\d)_", RegexOptions.Compiled);

	private static readonly Regex GradeStageRe = new Regex("_Grade(\\d+)", RegexOptions.Compiled);

	private static readonly Regex MirrorHeartStageRe = new Regex("_G_(\\d+)", RegexOptions.Compiled);

	private static readonly Regex OrdealLevelRe = new Regex("_T_(\\d+)_|^M_PD_.*_T_(\\d+)$", RegexOptions.Compiled);

	private static readonly Regex BareGradeRe = new Regex("_Grade(?!\\d)", RegexOptions.Compiled);

	public static string Of(string internalName)
	{
		ArgumentNullException.ThrowIfNull(internalName, "internalName");
		Match match = AwakenTierRe.Match(internalName);
		if (match.Success)
		{
			return "Awaken_" + match.Groups[1].Value;
		}
		Match match2 = FrozenLamentTierRe.Match(internalName);
		if (match2.Success)
		{
			return "FrozenLament_" + match2.Groups[1].Value;
		}
		if (internalName.Contains("_Easy", StringComparison.OrdinalIgnoreCase))
		{
			return "Easy";
		}
		if (internalName.Contains("_hard", StringComparison.OrdinalIgnoreCase))
		{
			return "Hard";
		}
		Match match3 = GradeStageRe.Match(internalName);
		if (match3.Success)
		{
			return "Grade" + match3.Groups[1].Value;
		}
		Match match4 = MirrorHeartStageRe.Match(internalName);
		if (match4.Success)
		{
			return "G_" + match4.Groups[1].Value;
		}
		Match match5 = OrdealLevelRe.Match(internalName);
		if (match5.Success)
		{
			return "T_" + (match5.Groups[1].Success ? match5.Groups[1].Value : match5.Groups[2].Value);
		}
		if (BareGradeRe.IsMatch(internalName))
		{
			return "Grade";
		}
		return "Base";
	}
}
