using System;
using AionFlex.Core.Models;

namespace AionFlex.Core.Schedules;

public static class GameServerClock
{
	public const string UnconfirmedReason = "Server time not known yet: log in on this datacenter once and the meter reads it from the game";

	public static string? TimeZoneIdOf(GameRegion region)
	{
		return region switch
		{
			GameRegion.TW => "Asia/Taipei", 
			GameRegion.KR => "Asia/Seoul", 
			GameRegion.EU => "Europe/Berlin", 
			_ => null, 
		};
	}

	public static ServerClockZone? ZoneFor(GameRegion region, TimeSpan? live, TimeSpan? remembered, DateTimeOffset now)
	{
		string text = TimeZoneIdOf(region);
		TimeZoneInfo timeZoneInfo = ((text == null) ? null : Resolve(text));
		if (live.HasValue)
		{
			TimeSpan valueOrDefault = live.GetValueOrDefault();
			if (timeZoneInfo != null && timeZoneInfo.GetUtcOffset(now) == valueOrDefault)
			{
				return new ServerClockZone(timeZoneInfo, text, FromGame: true, DisagreesWithNamedZone: false);
			}
			return new ServerClockZone(Fixed(valueOrDefault), OffsetLabel(valueOrDefault), FromGame: true, timeZoneInfo != null);
		}
		if (timeZoneInfo != null)
		{
			return new ServerClockZone(timeZoneInfo, text, FromGame: false, DisagreesWithNamedZone: false);
		}
		if (remembered.HasValue)
		{
			TimeSpan valueOrDefault2 = remembered.GetValueOrDefault();
			return new ServerClockZone(Fixed(valueOrDefault2), OffsetLabel(valueOrDefault2), FromGame: true, DisagreesWithNamedZone: false);
		}
		return null;
	}

	public static string OffsetLabel(TimeSpan offset)
	{
		return $"UTC{((offset < TimeSpan.Zero) ? "-" : "+")}{offset.Duration():hh\\:mm}";
	}

	private static TimeZoneInfo Fixed(TimeSpan offset)
	{
		string text = OffsetLabel(offset);
		return TimeZoneInfo.CreateCustomTimeZone("AionFlex server " + text, offset, text, text);
	}

	public static TimeZoneInfo? Resolve(string ianaId)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch (Exception ex) when ((ex is TimeZoneNotFoundException || ex is InvalidTimeZoneException) ? true : false)
		{
			string text = ianaId switch
			{
				"Asia/Taipei" => "Taipei Standard Time", 
				"Asia/Seoul" => "Korea Standard Time", 
				"Europe/Berlin" => "W. Europe Standard Time", 
				_ => null, 
			};
			if (text == null)
			{
				return null;
			}
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById(text);
			}
			catch (Exception ex2) when ((ex2 is TimeZoneNotFoundException || ex2 is InvalidTimeZoneException) ? true : false)
			{
				return null;
			}
		}
	}
}
