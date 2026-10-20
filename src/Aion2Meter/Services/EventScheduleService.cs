using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AionFlex.Application.Data;
using AionFlex.Core.Models;
using AionFlex.Core.Schedules;

namespace Aion2Meter.Services;

public sealed record MeterEventTimerSnapshot(
    string Key,
    string Kind,
    string Title,
    string Detail,
    DateTime LocalStart,
    TimeSpan TimeUntil,
    bool IsActive,
    string AccentColor);

public sealed record MeterEventAlertSnapshot(
    string Key,
    string Title,
    string Countdown,
    string Detail,
    string BackgroundColor,
    string BorderColor);

public sealed record MeterEventScheduleSnapshot(
    bool IsReady,
    string RegionText,
    string ClockText,
    string StatusText,
    IReadOnlyList<MeterEventTimerSnapshot> Timers,
    IReadOnlyList<MeterEventAlertSnapshot> Alerts);

/// <summary>
/// Local Aion 2 event calendar. Schedule rows come from the game data tables
/// distributed with the application; the live server offset comes from the
/// login packet already decoded by <see cref="DataRepository"/>.
/// </summary>
public sealed class EventScheduleService
{
    private static readonly TimeSpan AlertLead = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StartedWindow = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan RiftDuration = TimeSpan.FromMinutes(10);

    private readonly DataRepository _dataRepository;
    private readonly IReadOnlyDictionary<string, ServiceSchedule> _schedules;

    private sealed record ServiceSchedule(TimeOnly RiftFirst, int RiftEveryMinutes, IReadOnlyList<EventDefinition> Events);

    private sealed record EventDefinition(
        string Id,
        string Kind,
        string Name,
        string Location,
        DayOfWeek[] Days,
        TimeOnly[] Times,
        TimeSpan Duration);

    public EventScheduleService(DataRepository dataRepository, string filePath)
    {
        _dataRepository = dataRepository;
        _schedules = Load(filePath);
    }

    public MeterEventScheduleSnapshot GetSnapshot(int maximum = 16)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        if (!TryGetClock(utcNow, out GameRegion region, out ServerClockZone? clock))
        {
            return new MeterEventScheduleSnapshot(
                false,
                T("Region not detected", "Регион не определён"),
                T("Waiting for server clock", "Часы сервера ожидаются"),
                T("Log in to a character — the calendar synchronizes from a game packet", "Войдите на персонажа — календарь синхронизируется по игровому пакету"),
                [],
                []);
        }

        string service = region.ServiceOf();
        if (!_schedules.TryGetValue(service, out ServiceSchedule? schedule))
        {
            return new MeterEventScheduleSnapshot(
                false,
                region.DisplayName(),
                clock.Label,
                T("No local calendar is available for this region", "Для этого региона нет локального календаря"),
                [],
                []);
        }

        List<MeterEventTimerSnapshot> timers = BuildTimers(schedule, clock.Zone, utcNow)
            .Where(timer => timer.IsActive || timer.TimeUntil >= TimeSpan.Zero)
            .OrderBy(timer => timer.IsActive ? 0 : 1)
            .ThenBy(timer => timer.TimeUntil)
            .Take(Math.Max(1, maximum))
            .ToList();

        MeterEventAlertSnapshot[] alerts = timers
            .Where(timer => timer.IsActive || timer.TimeUntil <= AlertLead)
            .OrderBy(timer => timer.IsActive ? 0 : 1)
            .ThenBy(timer => timer.TimeUntil)
            .Take(6)
            .Select(CreateAlert)
            .ToArray();
        string status = timers.Count == 0
            ? T("No events in the next 24 hours", "На ближайшие сутки событий нет")
            : timers[0].IsActive
                ? T($"Now: {timers[0].Title}", $"Сейчас: {timers[0].Title}")
                : T($"Next: {timers[0].Title} · {Countdown(timers[0].TimeUntil)}", $"Ближайшее: {timers[0].Title} · {Countdown(timers[0].TimeUntil)}");

        return new MeterEventScheduleSnapshot(
            true,
            region.DisplayName(),
            T($"{clock.Label} · game time", $"{clock.Label} · игровое время"),
            status,
            timers,
            alerts);
    }

    private bool TryGetClock(DateTimeOffset utcNow, out GameRegion region, out ServerClockZone? clock)
    {
        (GameRegion Region, TimeSpan Offset, long SinceMs)? live = _dataRepository.GetServerClock();
        if (live.HasValue)
        {
            region = live.Value.Region;
            clock = GameServerClock.ZoneFor(region, live.Value.Offset, null, utcNow);
            return clock is not null;
        }

        int? serverId = _dataRepository.GetCurrentServerId();
        GameRegion? detected = serverId.HasValue
            ? GameRegionExtensions.GlobalDatacenterOf(serverId.Value)
            : null;
        if (!detected.HasValue)
        {
            // Aion 2 Global's EU schedule is a useful startup default and, unlike
            // a live 00E2/1536 clock packet, survives an application restart. The
            // moment the game supplies a server id or a clock reading, that exact
            // region/offset replaces this fallback.
            region = GameRegion.EU;
            clock = GameServerClock.ZoneFor(region, null, null, utcNow);
            return clock is not null;
        }

        region = detected.Value;
        clock = GameServerClock.ZoneFor(region, null, null, utcNow);
        return clock is not null;
    }

    private static IEnumerable<MeterEventTimerSnapshot> BuildTimers(
        ServiceSchedule schedule,
        TimeZoneInfo serverZone,
        DateTimeOffset utcNow)
    {
        DateTime serverDate = TimeZoneInfo.ConvertTime(utcNow, serverZone).Date;
        var timers = new List<MeterEventTimerSnapshot>();

        for (int dayOffset = -1; dayOffset <= 1; dayOffset++)
        {
            DateTime day = serverDate.AddDays(dayOffset);
            for (int minute = schedule.RiftFirst.Hour * 60 + schedule.RiftFirst.Minute;
                 minute < 24 * 60;
                 minute += Math.Max(1, schedule.RiftEveryMinutes))
            {
                DateTimeOffset start = ServerInstant(serverZone, day, new TimeOnly(minute / 60, minute % 60));
                AddTimer(timers, "rift", "rift", T("Rift", "Разлом"), T("Dimensional Rift", "Пространственный разлом"), start, RiftDuration, utcNow);
            }

            foreach (EventDefinition definition in schedule.Events)
            {
                if (!definition.Days.Contains(day.DayOfWeek))
                {
                    continue;
                }

                foreach (TimeOnly time in definition.Times)
                {
                    DateTimeOffset start = ServerInstant(serverZone, day, time);
                    AddTimer(
                        timers,
                        definition.Id,
                        definition.Kind,
                        TranslateName(definition.Name, definition.Kind),
                        definition.Location,
                        start,
                        definition.Duration <= TimeSpan.Zero ? StartedWindow : definition.Duration,
                        utcNow);
                }
            }
        }

        return timers;
    }

    private static void AddTimer(
        ICollection<MeterEventTimerSnapshot> timers,
        string id,
        string kind,
        string title,
        string detail,
        DateTimeOffset start,
        TimeSpan duration,
        DateTimeOffset utcNow)
    {
        DateTimeOffset end = start + duration;
        if (end <= utcNow - StartedWindow || start > utcNow + TimeSpan.FromDays(1))
        {
            return;
        }

        timers.Add(new MeterEventTimerSnapshot(
            $"{id}:{start.ToUnixTimeSeconds()}",
            KindLabel(kind),
            title,
            string.IsNullOrWhiteSpace(detail) ? KindLabel(kind) : detail,
            start.ToLocalTime().DateTime,
            start - utcNow,
            utcNow >= start && utcNow < end,
            Accent(kind)));
    }

    private static MeterEventAlertSnapshot CreateAlert(MeterEventTimerSnapshot timer)
    {
        (string background, string border) = timer.Kind switch
        {
            "RIFT" or "РИФТ" => ("#E63D68C9", "#FF6F9EFF"),
            "SIEGE" or "ОСАДА" => ("#E69F1D35", "#FFFF6676"),
            "ABYSS BOSS" or "БОСС БЕЗДНЫ" => ("#E65B21B6", "#FFB28AFF"),
            "FESTIVAL" or "ФЕСТИВАЛЬ" => ("#E60F766E", "#FF4EDACB"),
            "INVASION" or "ВТОРЖЕНИЕ" => ("#E69A4A12", "#FFFFA04D"),
            _ => ("#E63A4256", "#FF738399")
        };
        return new MeterEventAlertSnapshot(
            timer.Key,
            timer.IsActive
                ? T($"{timer.Title} is open", $"{timer.Title} открыт")
                : T($"{timer.Title} in", $"{timer.Title} через"),
            timer.IsActive ? T("NOW", "СЕЙЧАС") : Countdown(timer.TimeUntil),
            timer.Detail,
            background,
            border);
    }

    public static string Countdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        int totalSeconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds / 60 % 60;
        int seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }

    private static DateTimeOffset ServerInstant(TimeZoneInfo zone, DateTime day, TimeOnly time)
    {
        var wallClock = new DateTime(day.Year, day.Month, day.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(wallClock, zone.GetUtcOffset(wallClock));
    }

    private static string TranslateName(string name, string kind) => UiLanguageService.IsRussian
        ? name switch
        {
            "Artifact Siege" => "Осада артефакта",
            "Dimensional Invasion" => "Пространственное вторжение",
            "Shugo Festival" => "Фестиваль шуго",
            _ when kind == "siege" => "Осада артефакта",
            _ when kind == "invasion" => "Пространственное вторжение",
            _ when kind == "shugoFesta" => "Фестиваль шуго",
            _ => name
        }
        : name;

    private static string KindLabel(string kind) => kind switch
    {
        "rift" => T("RIFT", "РИФТ"),
        "siege" => T("SIEGE", "ОСАДА"),
        "abyssBoss" => T("ABYSS BOSS", "БОСС БЕЗДНЫ"),
        "shugoFesta" => T("FESTIVAL", "ФЕСТИВАЛЬ"),
        "invasion" => T("INVASION", "ВТОРЖЕНИЕ"),
        "fieldBoss" => T("FIELD BOSS", "ПОЛЕВОЙ БОСС"),
        _ => T("EVENT", "СОБЫТИЕ")
    };

    private static string Accent(string kind) => kind switch
    {
        "rift" => "#FF6196FF",
        "siege" => "#FFFF6676",
        "abyssBoss" => "#FFB28AFF",
        "shugoFesta" => "#FF4EDACB",
        "invasion" => "#FFFFA04D",
        "fieldBoss" => "#FFE5C15A",
        _ => "#FF67CED2"
    };

    private static IReadOnlyDictionary<string, ServiceSchedule> Load(string filePath)
    {
        var result = new Dictionary<string, ServiceSchedule>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return result;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!document.RootElement.TryGetProperty("services", out JsonElement services))
        {
            return result;
        }

        foreach (JsonProperty service in services.EnumerateObject())
        {
            TimeOnly first = new(2, 0);
            int everyMinutes = 180;
            if (service.Value.TryGetProperty("rift", out JsonElement rift))
            {
                if (rift.TryGetProperty("first", out JsonElement firstElement))
                {
                    TimeOnly.TryParseExact(firstElement.GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out first);
                }
                if (rift.TryGetProperty("everyMinutes", out JsonElement everyElement))
                {
                    everyMinutes = everyElement.GetInt32();
                }
            }

            var events = new List<EventDefinition>();
            if (service.Value.TryGetProperty("events", out JsonElement eventArray))
            {
                foreach (JsonElement item in eventArray.EnumerateArray())
                {
                    string id = item.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                    string kind = item.TryGetProperty("kind", out JsonElement kindElement) ? kindElement.GetString() ?? string.Empty : string.Empty;
                    string name = ReadEnglish(item, "name");
                    string location = ReadEnglish(item, "location");
                    DayOfWeek[] days = item.GetProperty("days").EnumerateArray()
                        .Select(day => ParseDay(day.GetString()))
                        .Where(day => day.HasValue)
                        .Select(day => day!.Value)
                        .ToArray();
                    TimeOnly[] times = item.GetProperty("times").EnumerateArray()
                        .Select(time => TimeOnly.ParseExact(time.GetString()!, "HH:mm", CultureInfo.InvariantCulture))
                        .ToArray();
                    int durationSeconds = item.TryGetProperty("durationSeconds", out JsonElement duration)
                        ? duration.GetInt32()
                        : 0;
                    if (days.Length > 0 && times.Length > 0)
                    {
                        events.Add(new EventDefinition(id, kind, name, location, days, times, TimeSpan.FromSeconds(durationSeconds)));
                    }
                }
            }

            result[service.Name] = new ServiceSchedule(first, everyMinutes, events);
        }

        return result;
    }

    private static string ReadEnglish(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }
        return value.TryGetProperty("en", out JsonElement english) ? english.GetString() ?? string.Empty : string.Empty;
    }

    private static DayOfWeek? ParseDay(string? day) => day switch
    {
        "Sun" => DayOfWeek.Sunday,
        "Mon" => DayOfWeek.Monday,
        "Tue" => DayOfWeek.Tuesday,
        "Wed" => DayOfWeek.Wednesday,
        "Thu" => DayOfWeek.Thursday,
        "Fri" => DayOfWeek.Friday,
        "Sat" => DayOfWeek.Saturday,
        _ => null
    };

    private static string T(string english, string russian) => UiLanguageService.Pick(english, russian);
}
