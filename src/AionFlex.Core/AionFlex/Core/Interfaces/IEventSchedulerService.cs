using System;
using System.Collections.Generic;
using AionFlex.Core.Models;
using AionFlex.Core.Schedules;

namespace AionFlex.Core.Interfaces;

public interface IEventSchedulerService
{
	bool SetLiveServerClock(GameRegion region, TimeSpan offset);

	bool SetRememberedServerClock(GameRegion region, TimeSpan offset);

	ServerClockZone? GetServerClock(GameRegion region);

	RiftScheduleData? GetScheduleForRegion(GameRegion region);

	IReadOnlyList<RiftOccurrence> GetTodayOccurrences(GameRegion region);

	IReadOnlyList<RiftOccurrence> GetUpcomingRifts(GameRegion region, int daysAhead);

	RiftOccurrence? GetNextRift(GameRegion region);

	bool IsRiftActive(GameRegion region);

	GameRegion GetRegionForLanguage(int language);

	IReadOnlyList<GameEvent> GetEventsForRegion(GameRegion region);

	IReadOnlyList<EventOccurrence> GetUpcomingEventOccurrences(GameRegion region, int daysAhead = 1);
}
