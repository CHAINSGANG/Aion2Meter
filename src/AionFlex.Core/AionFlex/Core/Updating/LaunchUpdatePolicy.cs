using System;

namespace AionFlex.Core.Updating;

public static class LaunchUpdatePolicy
{
	public static readonly TimeSpan MaxAnswerDelay = TimeSpan.FromSeconds(30.0);

	public static readonly TimeSpan RetryAutoInstallAfter = TimeSpan.FromHours(6.0);

	public static LaunchUpdateAction Decide(bool updateAvailable, string latestVersion, TimeSpan sinceLaunch, string? lastAutoInstallVersion, DateTime? lastAutoInstallUtc, DateTime nowUtc)
	{
		if (!updateAvailable)
		{
			return LaunchUpdateAction.None;
		}
		if (sinceLaunch > MaxAnswerDelay)
		{
			return LaunchUpdateAction.None;
		}
		if (lastAutoInstallUtc.HasValue)
		{
			DateTime valueOrDefault = lastAutoInstallUtc.GetValueOrDefault();
			if (string.Equals(lastAutoInstallVersion, latestVersion, StringComparison.Ordinal) && nowUtc - valueOrDefault < RetryAutoInstallAfter)
			{
				return LaunchUpdateAction.Ask;
			}
		}
		return LaunchUpdateAction.Install;
	}
}
