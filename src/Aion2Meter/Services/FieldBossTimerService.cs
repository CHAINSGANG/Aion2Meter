using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AionFlex.Core.Models;

namespace Aion2Meter.Services;

/// <summary>
/// Keeps the game's live map timer for Immortal Gartua. The map packet is the
/// authority: there is no guessed spawn cycle or dependency on another meter.
/// </summary>
public sealed class FieldBossTimerService
{
    internal const int GartuaMapId = 1110;
    internal const int GartuaRowId = 111021;
    private static readonly TimeSpan AlertLead = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ExpectedWindow = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan AliveFreshness = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);
    private const string Accent = "#FFE5C15A";

    private readonly object _gate = new();
    private readonly string _cachePath;
    private CachedTimer? _timer;

    private sealed record CachedTimer(int ServerId, int MapId, int RowId, bool Alive, long TimeUtcMs, long SeenUtcMs);

    public FieldBossTimerService(string cachePath)
    {
        _cachePath = cachePath;
        try
        {
            if (File.Exists(cachePath))
                _timer = JsonSerializer.Deserialize<CachedTimer>(File.ReadAllText(cachePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A corrupt or inaccessible cache must not prevent packet capture.
        }
    }

    public bool Record(FieldBossListEvent list, int? serverId)
    {
        if (serverId is not > 0 || list.MapId != GartuaMapId)
            return false;

        FieldBossEntry? entry = list.Entries.FirstOrDefault(item => item.RowId == GartuaRowId);
        if (entry is null)
            return false;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (entry.TimeUtcMs < 1_672_531_200_000L ||
            entry.TimeUtcMs > nowMs + (long)CacheLifetime.TotalMilliseconds)
            return false;

        var reading = new CachedTimer(serverId.Value, list.MapId, entry.RowId, entry.Alive, entry.TimeUtcMs, nowMs);
        lock (_gate)
            _timer = reading;

        try
        {
            string? directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = _cachePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(reading));
            File.Move(temporaryPath, _cachePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The live in-memory timer still works if persistence fails.
        }
        return true;
    }

    public (MeterEventTimerSnapshot Timer, MeterEventAlertSnapshot? Alert)? GetSnapshot(int? serverId) =>
        GetSnapshotAt(DateTimeOffset.UtcNow, serverId);

    internal (MeterEventTimerSnapshot Timer, MeterEventAlertSnapshot? Alert)? GetSnapshotAt(
        DateTimeOffset now, int? serverId)
    {
        CachedTimer? reading;
        lock (_gate)
            reading = _timer;

        long nowMs = now.ToUnixTimeMilliseconds();
        if (reading is null || serverId != reading.ServerId ||
            reading.MapId != GartuaMapId || reading.RowId != GartuaRowId ||
            reading.SeenUtcMs > nowMs + 60_000 ||
            nowMs - reading.SeenUtcMs > CacheLifetime.TotalMilliseconds ||
            reading.TimeUtcMs < 1_672_531_200_000L ||
            reading.TimeUtcMs > reading.SeenUtcMs + CacheLifetime.TotalMilliseconds)
            return null;

        DateTimeOffset spawn = DateTimeOffset.FromUnixTimeMilliseconds(reading.TimeUtcMs);
        TimeSpan remaining = spawn - now;
        bool confirmedAlive = reading.Alive && nowMs - reading.SeenUtcMs <= AliveFreshness.TotalMilliseconds;
        bool expectedNow = !reading.Alive && remaining <= TimeSpan.Zero && -remaining <= ExpectedWindow;
        if (!confirmedAlive && (reading.Alive || remaining < -ExpectedWindow))
            return null;

        string name = UiLanguageService.Pick("Immortal Gartua", "Бессмертный Гартуа");
        string detail = UiLanguageService.Pick("Live map timer", "Таймер игровой карты");
        string key = $"field-boss:{reading.ServerId}:{GartuaRowId}:{reading.TimeUtcMs}";
        var timer = new MeterEventTimerSnapshot(
            key,
            UiLanguageService.Pick("FIELD BOSS", "ПОЛЕВОЙ БОСС"),
            name,
            detail,
            spawn.ToLocalTime().DateTime,
            remaining,
            confirmedAlive || expectedNow,
            Accent);

        MeterEventAlertSnapshot? alert = null;
        if (confirmedAlive || expectedNow || remaining <= AlertLead)
        {
            string title = confirmedAlive
                ? UiLanguageService.Pick("Immortal Gartua is up", "Бессмертный Гартуа появился")
                : expectedNow
                    ? UiLanguageService.Pick("Immortal Gartua expected", "Ожидается Бессмертный Гартуа")
                    : UiLanguageService.Pick("Immortal Gartua in", "Бессмертный Гартуа через");
            alert = new MeterEventAlertSnapshot(
                key,
                title,
                confirmedAlive || expectedNow
                    ? UiLanguageService.Pick("NOW", "СЕЙЧАС")
                    : EventScheduleService.Countdown(remaining),
                detail,
                Accent);
        }
        return (timer, alert);
    }
}
