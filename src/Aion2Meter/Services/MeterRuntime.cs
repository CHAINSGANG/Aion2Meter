using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AionFlex.Application.Data;
using AionFlex.Application.DpsCalculation;
using AionFlex.Application.HpTracking;
using AionFlex.Application.Mobs;
using AionFlex.Application.Skills;
using AionFlex.Core.Combat;
using AionFlex.Core.Models;
using AionFlex.Infrastructure.PacketCapture;
using AionFlex.Infrastructure.PacketParsing.Dps;
using AionFlex.Infrastructure.PacketParsing.Features.Pet;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion2Meter.Services;

public enum MeterDisplayMode
{
    Damage,
    Healing,
    DamageTaken
}

public enum TargetFrameKind
{
    Normal,
    Elite,
    Hero,
    Legend
}

public sealed record MeterPlayerSnapshot(
    int PlayerId,
    string Nickname,
    JobClass Job,
    double Dps,
    long Damage,
    double Share,
    double CritRate,
    int Hits,
    double MitigationRate,
    IReadOnlyList<MeterSkillSnapshot> Skills);

public sealed record MeterSkillSnapshot(
    int SkillCode,
    string Name,
    double Dps,
    long Damage,
    double Share,
    int Hits,
    double CritRate,
    double BackRate,
    double DoubleRate,
    double PerfectRate,
    double AverageDamage,
    long MaximumDamage);

public sealed record MeterDebuffSnapshot(
    int SkillCode,
    string Name,
    long RemainingMs,
    long DurationMs,
    int Stacks);

public sealed record MeterNavigationPosition(
    int ZoneId,
    double X,
    double Y,
    DateTime ObservedAtUtc);

public sealed record MeterSnapshot(
    string TargetName,
    TargetFrameKind TargetFrame,
    double? TargetHpPercent,
    long? TargetCurrentHp,
    long? TargetMaxHp,
    long BattleTimeMs,
    double TotalDps,
    long TotalDamage,
    IReadOnlyList<MeterPlayerSnapshot> Players,
    bool IsInCombat,
    long ReceivedPackets,
    long DroppedPackets,
    int? LocalPlayerId,
    string? LocalPlayerName,
    JobClass LocalPlayerJob,
    EnrageTimerView EnrageTimer);

public sealed class MeterRuntime : IDisposable
{
    private static readonly Regex LocalPlayerLogPattern = new(
        @"\[PLAYER\] local id=(?<id>\d+) name='(?<name>[^']+)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DataRepository _dataRepository;
    private readonly SkillRepository _skillRepository;
    private readonly MobRepository _mobRepository;
    private readonly NpcapCaptureService _capture;
    private readonly DpsPacketParser _parser;
    private readonly PetPacketParser _petParser;
    private readonly DpsCalculator _calculator;
    private readonly HpTracker _hpTracker;
    private readonly DiagnosticLog _diagnostics;
    private readonly PetCatalog _petCatalog;
    private readonly EventScheduleService _eventSchedule;
    private readonly MitigationTracker _mitigationTracker = new();
    private readonly DebuffTracker _debuffTracker = new();
    private readonly ConcurrentDictionary<int, int> _playerTargets = new();
    private readonly ConcurrentDictionary<int, string> _identityDisplayTrace = new();
    private readonly object _featureStateLock = new();
    private IReadOnlyList<(int PetId, int Level)> _petRoster = [];
    private PartyRoomRoster? _partyRoster;
    private BossPullClock _bossPullClock = new(30_000L);
    private int _selectedTargetId;
    private int _lastCombatTargetId;
    private int _displayTargetId;
    private int _inferredLocalPlayerId;
    private int _hasLiveLocalPlayerIdentity;
    private string _lastHealthTrace = string.Empty;
    private string _lastDebuffTrace = string.Empty;
    private string _lastEventTrace = string.Empty;
    private int _currentZoneId;
    private MeterNavigationPosition? _navigationPosition;
    private bool _disposed;

    private MeterRuntime(
        DataRepository dataRepository,
        SkillRepository skillRepository,
        MobRepository mobRepository,
        NpcapCaptureService capture,
        DpsPacketParser parser,
        PetPacketParser petParser,
        DpsCalculator calculator,
        HpTracker hpTracker,
        DiagnosticLog diagnostics,
        PetCatalog petCatalog,
        EventScheduleService eventSchedule)
    {
        _dataRepository = dataRepository;
        _skillRepository = skillRepository;
        _mobRepository = mobRepository;
        _capture = capture;
        _parser = parser;
        _petParser = petParser;
        _calculator = calculator;
        _hpTracker = hpTracker;
        _diagnostics = diagnostics;
        _petCatalog = petCatalog;
        _eventSchedule = eventSchedule;
    }

    public static async Task<MeterRuntime> StartAsync()
    {
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        string DataFile(string name) => Path.Combine(dataDirectory, name);

        if (!File.Exists(DataFile("skills.json")) || !File.Exists(DataFile("mobs.json")))
        {
            throw new FileNotFoundException("Не найдены игровые справочники в папке data.");
        }

        var dataRepository = new DataRepository(NullLogger<DataRepository>.Instance);
        var skillRepository = new SkillRepository(
            NullLogger<SkillRepository>.Instance,
            skillsFilePath: DataFile("skills.json"),
            npcSkillsFilePath: DataFile("npc_skills.json"),
            theostonesFilePath: DataFile("theostones.json"),
            skillEffectTypesFilePath: DataFile("skill_effect_types.json"),
            summonSkillsFilePath: DataFile("summon_skills.json"));
        var mobRepository = new MobRepository(
            NullLogger<MobRepository>.Instance,
            mobsFilePath: DataFile("mobs.json"),
            npcSummonsFilePath: DataFile("npc_summons.json"));

        await Task.WhenAll(skillRepository.InitializeAsync(), mobRepository.ReloadAsync()).ConfigureAwait(false);

        var capture = new NpcapCaptureService(NullLogger<NpcapCaptureService>.Instance);
        var parser = new DpsPacketParser(
            NullLogger<DpsPacketParser>.Instance,
            dataRepository,
            skillRepository,
            capture);
        var petParser = new PetPacketParser(NullLogger<PetPacketParser>.Instance);
        var calculator = new DpsCalculator(
            NullLogger<DpsCalculator>.Instance,
            dataRepository,
            skillRepository,
            mobRepository);
        var hpTracker = new HpTracker();
        string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        var diagnostics = new DiagnosticLog(logDirectory);
        (int EntityId, string Name)? recoveredLocal = TryRecoverLocalPlayer(logDirectory);
        if (recoveredLocal.HasValue)
        {
            dataRepository.SetLocalPlayerName(recoveredLocal.Value.Name);
            dataRepository.SetNickname(
                recoveredLocal.Value.EntityId,
                recoveredLocal.Value.Name,
                isConfirmedPlayer: true,
                isAuthoritativeSource: true);
            diagnostics.Write(
                "PLAYER",
                $"restored local id={recoveredLocal.Value.EntityId} name='{recoveredLocal.Value.Name}' from current game session");
        }
        hpTracker.Diagnostic += message => diagnostics.Write("HP-TRACKER", message);
        calculator.SetMaxHpProvider(id => hpTracker.GetActorHpData(id)?.MaxHp ?? 0);
        calculator.SetTrustedMaxHpProvider((id, firstDamageMs) => hpTracker.TryGetTrustedMaxHp(id, firstDamageMs));
        calculator.SetHpProgressProvider(id => hpTracker.TryGetHpProgress(id));
        var petCatalog = PetCatalog.Load(DataFile("pets.json"), DataFile("pet_stats.json"));
        var eventSchedule = new EventScheduleService(dataRepository, DataFile("event_schedules.json"));

        var runtime = new MeterRuntime(dataRepository, skillRepository, mobRepository, capture, parser, petParser, calculator, hpTracker, diagnostics, petCatalog, eventSchedule);
        if (recoveredLocal.HasValue)
        {
            Volatile.Write(ref runtime._inferredLocalPlayerId, recoveredLocal.Value.EntityId);
        }
        runtime.WirePipeline();

        try
        {
            capture.Start();
            diagnostics.Write(
                "CAPTURE",
                $"started devices={capture.ActiveDeviceCount} mode={capture.CaptureMode} elevated={IsProcessElevated()}");
            return runtime;
        }
        catch
        {
            runtime.Dispose();
            throw;
        }
    }

    private static (int EntityId, string Name)? TryRecoverLocalPlayer(string logDirectory)
    {
        DateTime? gameStartedUtc = GetCurrentGameSessionStartUtc();
        if (!gameStartedUtc.HasValue || !Directory.Exists(logDirectory))
        {
            return null;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(logDirectory, "hp-debug-*.log")
                         .Where(file => File.GetLastWriteTimeUtc(file) >= gameStartedUtc.Value.AddSeconds(-5))
                         .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch (IOException)
                {
                    // The diagnostic file created for this very startup is still
                    // locked for writing. Skip it and inspect the previous run.
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                for (int index = lines.Length - 1; index >= 0; index--)
                {
                    Match match = LocalPlayerLogPattern.Match(lines[index]);
                    if (match.Success &&
                        int.TryParse(match.Groups["id"].Value, out int entityId) &&
                        entityId > 0)
                    {
                        string name = match.Groups["name"].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            return (entityId, name);
                        }
                    }
                }
            }
        }
        catch
        {
            // Recovery is an optimization. Fresh login packets remain the
            // authoritative fallback if old diagnostics cannot be read.
        }

        return null;
    }

    private static DateTime? GetCurrentGameSessionStartUtc()
    {
        try
        {
            DateTime[] starts = Process.GetProcessesByName("AION2")
                .Select(process =>
                {
                    using (process)
                    {
                        try
                        {
                            return (DateTime?)process.StartTime.ToUniversalTime();
                        }
                        catch
                        {
                            return null;
                        }
                    }
                })
                .Where(start => start.HasValue)
                .Select(start => start!.Value)
                .ToArray();
            return starts.Length == 0 ? null : starts.Min();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsProcessElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void WirePipeline()
    {
        _capture.PayloadCaptured += (_, payload) =>
        {
            _parser.ProcessPayload(payload);
            _petParser.ProcessPayload(payload);
        };

        _petParser.PetRosterDiscovered += (_, pets) =>
        {
            lock (_featureStateLock)
            {
                _petRoster = pets.ToArray();
            }
        };

        _parser.PartyRoomRosterParsed += (_, party) =>
        {
            lock (_featureStateLock)
            {
                _partyRoster = party.Roster;
            }
        };

        _parser.DamageEventParsed += (_, damageEvent) =>
        {
            _mitigationTracker.Record(damageEvent);
            _bossPullClock.OnDamage(
                damageEvent.TargetId,
                damageEvent.ActorId,
                damageEvent.Damage,
                damageEvent.IsHeal,
                damageEvent.Timestamp);
            if (damageEvent.IsHeal)
            {
                _dataRepository.AppendHealEvent(damageEvent);
            }
            else
            {
                int sourceId = _dataRepository.GetSummoner(damageEvent.ActorId) ?? damageEvent.ActorId;
                int knownLocalId = Volatile.Read(ref _inferredLocalPlayerId);
                bool playerSource = sourceId == knownLocalId ||
                    _dataRepository.IsConfirmedPlayer(sourceId) ||
                    !string.IsNullOrWhiteSpace(_dataRepository.GetNickname(sourceId));
                if (playerSource && IsHostileTargetCandidate(damageEvent.TargetId))
                {
                    Volatile.Write(ref _lastCombatTargetId, damageEvent.TargetId);
                }
                _diagnostics.Write(
                    "DAMAGE",
                    $"actor={damageEvent.ActorId} owner={sourceId} target={damageEvent.TargetId} amount={damageEvent.Damage} skill={damageEvent.SkillCode} playerSource={playerSource} ts={damageEvent.Timestamp}");
                _dataRepository.AppendDamageEvent(damageEvent);
            }
        };
        _parser.DefenseEventParsed += (_, defenseEvent) =>
        {
            _mitigationTracker.Record(defenseEvent);
            _diagnostics.Write(
                "DEFENSE",
                $"actor={defenseEvent.ActorId} target={defenseEvent.TargetId} skill={defenseEvent.SkillCode} outcome={defenseEvent.Outcome} counter={defenseEvent.EventCounter} ts={defenseEvent.Timestamp}");
        };
        _parser.AbnormalEventParsed += (_, abnormalEvent) =>
        {
            _debuffTracker.Record(abnormalEvent);
            _diagnostics.Write(
                "ABNORMAL",
                $"target={abnormalEvent.TargetId} slot={abnormalEvent.Slot} abnormal={abnormalEvent.AbnormalId} duration={abnormalEvent.DurationMs} caster={abnormalEvent.CasterId} sourceSkill={abnormalEvent.SourceSkillId} refresh={abnormalEvent.IsRefresh} remove={abnormalEvent.IsRemove} ts={abnormalEvent.Timestamp}");
        };
        _parser.HealTickParsed += (_, healEvent) => _dataRepository.AppendHealEvent(healEvent);
        _parser.NicknameDiscovered += (_, player) =>
        {
            _dataRepository.SetNickname(player.PlayerId, player.Nickname, player.IsConfirmedPlayer);
            _diagnostics.Write(
                "PLAYER",
                $"name id={player.PlayerId} nickname='{player.Nickname}' confirmed={player.IsConfirmedPlayer} server={player.ServerId}");
        };
        _parser.LocalPlayerDiscovered += (_, player) =>
        {
            _dataRepository.SetLocalPlayerName(player.Name);
            _dataRepository.SetNickname(player.EntityId, player.Name, true, true);
            Volatile.Write(ref _inferredLocalPlayerId, player.EntityId);
            Volatile.Write(ref _hasLiveLocalPlayerIdentity, 1);
            _diagnostics.Write("PLAYER", $"local id={player.EntityId} name='{player.Name}'");
            if (_playerTargets.TryGetValue(player.EntityId, out int targetId))
            {
                Volatile.Write(ref _selectedTargetId, targetId);
                _parser.SetCurrentTarget(targetId);
                _diagnostics.Write("TARGET", $"restored local target={targetId} after player discovery");
            }
        };
        _parser.MobNameDiscovered += (_, mob) =>
        {
            _dataRepository.SetMobName(mob.TargetId, mob.Name);
            _diagnostics.Write("NAME", $"entity={mob.TargetId} name='{mob.Name}'");
        };
        _hpTracker.BossHpReset += (_, reset) =>
            _bossPullClock.OnBackToFull(reset.EntityId, reset.TimestampMs);
        _parser.HpUpdateEventParsed += (_, hp) =>
        {
            _diagnostics.Write(
                "HP-RAW",
                $"actor={hp.ActorId} source={hp.Source} current={hp.CurrentHp} max={hp.MaxHp?.ToString() ?? "-"} framedMax={hp.FramedSpawnMaxHp?.ToString() ?? "-"} spawnNpc={hp.SpawnNpcId?.ToString() ?? "-"} ts={hp.Timestamp}");
            _hpTracker.ProcessHpUpdate(hp);
        };
        _parser.NpcTemplateDiscovered += (_, npc) =>
        {
            _dataRepository.SetEntityZKey(npc.EntityId, npc.TemplateCode);
            _dataRepository.SetEntityNpcId(npc.EntityId, npc.TemplateCode);
            string? name = _mobRepository.GetMobName(npc.TemplateCode);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _dataRepository.SetMobName(npc.EntityId, name);
            }

            uint catalogMaximum = _mobRepository.GetMaxHp(npc.TemplateCode);
            if (catalogMaximum > 0)
            {
                _hpTracker.SetMaxHp(npc.EntityId, catalogMaximum);
            }

            _diagnostics.Write(
                "NPC",
                $"template entity={npc.EntityId} npc={npc.TemplateCode} name='{name ?? "-"}' catalogMax={catalogMaximum}");
        };
        _parser.NpcAppeared += (_, npc) =>
        {
            _diagnostics.Write(
                "NPC",
                $"appeared entity={npc.EntityId} npc={npc.NpcId} dispatch={npc.FromDispatch} ts={npc.TimestampMs}");
            _bossPullClock.OnNewLife(npc.EntityId);
            _hpTracker.ResetActor(npc.EntityId);
            _dataRepository.StartEntityAppearance(npc.EntityId, npc.TimestampMs);
            _dataRepository.SetEntityNpcId(npc.EntityId, npc.NpcId);
        };
        _parser.SummonDetected += (_, summon) => _dataRepository.RegisterSummon(summon.SummonId, summon.SummonerId);
        _parser.SummonAppeared += (_, summon) =>
        {
            _dataRepository.RegisterSummon(summon.EntityId, summon.OwnerId);
            _dataRepository.StartEntityAppearance(summon.EntityId, summon.TimestampMs);
        };
        _parser.EntityRemoved += (_, entity) =>
        {
            _diagnostics.Write("NPC", $"removed entity={entity.EntityId} reason={entity.Reason} ts={entity.TimestampMs}");
            _dataRepository.EndSummonLife(entity.EntityId, entity.TimestampMs);
            _hpTracker.ResetActor(entity.EntityId);
            Interlocked.CompareExchange(ref _selectedTargetId, 0, entity.EntityId);
            Interlocked.CompareExchange(ref _lastCombatTargetId, 0, entity.EntityId);
        };
        _parser.BossEnrageDetected += (_, enrage) =>
            _bossPullClock.OnEnrage(enrage.EntityId, enrage.Timestamp);
        _parser.LocalServerIdResolved += (_, serverId) => _dataRepository.SetCurrentServerId(serverId);
        _parser.ZoneChanged += (_, zoneId) =>
        {
            lock (_featureStateLock)
            {
                _currentZoneId = zoneId;
                _navigationPosition = null;
            }
            _diagnostics.Write("MAP", $"zone={zoneId}");
        };
        _parser.LocalPositionParsed += (_, position) =>
        {
            int? localPlayerId = _dataRepository.GetLocalPlayerEntityId()
                ?? _dataRepository.GetLastBoundLocalPlayerEntityId()
                ?? PositiveOrNull(Volatile.Read(ref _inferredLocalPlayerId));
            if (position.EntityId > 0 && localPlayerId.HasValue && position.EntityId != localPlayerId.Value)
            {
                return;
            }

            lock (_featureStateLock)
            {
                _navigationPosition = new MeterNavigationPosition(
                    _currentZoneId,
                    position.X,
                    position.Y,
                    DateTime.UtcNow);
            }
        };
        _parser.PlayerTargetChanged += (_, target) =>
        {
            _playerTargets[target.PlayerEntityId] = target.TargetEntityId;
            int? localPlayerId = _dataRepository.GetLocalPlayerEntityId()
                ?? _dataRepository.GetLastBoundLocalPlayerEntityId();
            if (!localPlayerId.HasValue)
            {
                int inferred = Volatile.Read(ref _inferredLocalPlayerId);
                if (inferred <= 0)
                {
                    Interlocked.CompareExchange(ref _inferredLocalPlayerId, target.PlayerEntityId, 0);
                    inferred = Volatile.Read(ref _inferredLocalPlayerId);
                    _diagnostics.Write("PLAYER", $"provisional local id={inferred} from target-change lane");
                }
                localPlayerId = inferred;
            }
            bool isLocal = localPlayerId == target.PlayerEntityId;
            _diagnostics.Write(
                "TARGET",
                $"player={target.PlayerEntityId} target={target.TargetEntityId} local={localPlayerId?.ToString() ?? "-"} accepted={isLocal}");
            if (isLocal)
            {
                Volatile.Write(ref _selectedTargetId, target.TargetEntityId);
                _dataRepository.SetCurrentTarget(target.TargetEntityId);
                _parser.SetCurrentTarget(target.TargetEntityId);
            }
        };
    }

    public void SetBossOnlyMode(bool enabled) => _calculator.SetBossOnlyMode(enabled);

    public IReadOnlyList<MeterPetSnapshot> GetPets()
    {
        lock (_featureStateLock)
        {
            return _petRoster
                .Select(pet => _petCatalog.Resolve(pet.PetId, pet.Level))
                .OrderBy(pet => pet.Family, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(pet => pet.Level)
                .ThenBy(pet => pet.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    public PartyRoomRoster? GetPartyRoster()
    {
        lock (_featureStateLock)
        {
            return _partyRoster;
        }
    }

    public MeterNavigationPosition? GetNavigationPosition()
    {
        lock (_featureStateLock)
        {
            return _navigationPosition;
        }
    }

    public MeterEventScheduleSnapshot GetEventSchedule(int maximum = 16)
    {
        MeterEventScheduleSnapshot snapshot = _eventSchedule.GetSnapshot(maximum);
        string trace =
            $"ready={snapshot.IsReady} region='{snapshot.RegionText}' clock='{snapshot.ClockText}' " +
            $"timers={snapshot.Timers.Count} alert='{snapshot.Alert?.Title ?? "-"}' countdown='{snapshot.Alert?.Countdown ?? "-"}'";
        if (!string.Equals(trace, _lastEventTrace, StringComparison.Ordinal))
        {
            _lastEventTrace = trace;
            _diagnostics.Write("EVENT", trace);
        }
        return snapshot;
    }

    public void Reset()
    {
        _calculator.Reset();
        _hpTracker.ResetAll();
        _mitigationTracker.Clear();
        _debuffTracker.Clear();
        _bossPullClock = new BossPullClock(30_000L);
    }

    public IReadOnlyList<MeterDebuffSnapshot> GetActiveTargetDebuffs()
    {
        // Use the same target as the visible HP bar.  Selected-target packets can
        // briefly point at a party member or an add while the encounter header is
        // still showing the boss.
        int targetId = Volatile.Read(ref _displayTargetId);
        if (targetId <= 0)
        {
            targetId = FirstHostileTarget(
                Volatile.Read(ref _selectedTargetId),
                Volatile.Read(ref _lastCombatTargetId));
        }

        int? localPlayerId = _dataRepository.GetLocalPlayerEntityId()
            ?? _dataRepository.GetLastBoundLocalPlayerEntityId()
            ?? PositiveOrNull(Volatile.Read(ref _inferredLocalPlayerId));
        if (targetId <= 0 || !localPlayerId.HasValue)
        {
            TraceDebuffSelection(targetId, localPlayerId, 0, 0, []);
            return [];
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IReadOnlyList<ActiveDebuffWindow> active = _debuffTracker.GetActive(targetId, nowMs);
        var owned = active
            .Select(effect => new
            {
                Effect = effect,
                OwnerId = _dataRepository.GetSummoner(effect.CasterId) ?? effect.CasterId,
                SkillCode = effect.SourceSkillId / 10000 * 10000,
                Metadata = _skillRepository.GetSkillMetadata(effect.SourceSkillId)
            })
            .Where(item => item.OwnerId == localPlayerId.Value)
            .ToArray();
        MeterDebuffSnapshot[] debuffs = owned
            // Very short abnormal windows are hit markers used by the protocol,
            // not debuffs a player can meaningfully track in the HUD.
            .Where(item => item.SkillCode > 0
                && item.Metadata?.IsDebuff == true
                && item.Effect.DurationMs >= 750)
            .GroupBy(item => item.SkillCode)
            .Select(group =>
            {
                var longest = group.OrderByDescending(item => item.Effect.EndMs).First();
                int stacks = group
                    .GroupBy(item => item.Effect.AbnormalId)
                    .Select(items => items.Count())
                    .DefaultIfEmpty(1)
                    .Max();
                return new MeterDebuffSnapshot(
                    group.Key,
                    _skillRepository.GetSkillName(group.Key),
                    Math.Max(0, longest.Effect.EndMs - nowMs),
                    longest.Effect.DurationMs,
                    stacks);
            })
            .OrderBy(effect => effect.RemainingMs)
            .ThenBy(effect => effect.SkillCode)
            .Take(8)
            .ToArray();
        TraceDebuffSelection(targetId, localPlayerId, active.Count, owned.Length, debuffs);
        return debuffs;
    }

    private void TraceDebuffSelection(
        int targetId,
        int? localPlayerId,
        int activeCount,
        int ownedCount,
        IReadOnlyList<MeterDebuffSnapshot> debuffs)
    {
        string trace =
            $"target={targetId} local={localPlayerId?.ToString() ?? "-"} active={activeCount} mine={ownedCount} " +
            $"shown={debuffs.Count} skills=[{string.Join(',', debuffs.Select(item => item.SkillCode))}]";
        if (string.Equals(trace, _lastDebuffTrace, StringComparison.Ordinal))
        {
            return;
        }

        _lastDebuffTrace = trace;
        _diagnostics.Write("DEBUFFS", trace);
    }

    public MeterSnapshot GetSnapshot(MeterDisplayMode mode = MeterDisplayMode.Damage)
    {
        DpsSnapshot snapshot = mode switch
        {
            MeterDisplayMode.Healing => _calculator.CalculateHealing(HealingFightWindow.Unbounded),
            MeterDisplayMode.DamageTaken => _calculator.CalculateDamageTaken(),
            _ => _calculator.CalculateDps()
        };
        MeterPlayerSnapshot[] players = snapshot.GetPlayersByDps()
            .Select(player =>
            {
                IReadOnlyList<MeterSkillSnapshot> skills = player.GetSkillsByDamage()
                    .Select(skill => new MeterSkillSnapshot(
                        skill.SkillCode,
                        skill.SkillName,
                        skill.DpsContribution,
                        skill.TotalDamage,
                        skill.DamagePercentage,
                        skill.TotalHits,
                        skill.CritRate,
                        skill.BackRate,
                        skill.DoubleRate,
                        skill.PerfectRate,
                        skill.AverageDamage,
                        skill.GuardedOverallMax))
                    .ToArray();
                return new MeterPlayerSnapshot(
                    player.PlayerId,
                    player.Nickname,
                    player.Job,
                    player.Dps,
                    player.TotalDamage,
                    player.PartyDamageShare,
                    player.CritRate,
                    player.TotalHits,
                    _mitigationTracker.GetRate(player.PlayerId),
                    skills);
            })
            .ToArray();
        ResolveMissingPlayerNames(players);
        (long received, long dropped) = _capture.GetCaptureStatistics();
        int? localPlayerId = _dataRepository.GetLocalPlayerEntityId()
            ?? _dataRepository.GetLastBoundLocalPlayerEntityId()
            ?? PositiveOrNull(Volatile.Read(ref _inferredLocalPlayerId));
        string? localPlayerName = _calculator.GetLocalPlayerDisplayName();
        if (string.IsNullOrWhiteSpace(localPlayerName) && localPlayerId.HasValue)
        {
            localPlayerName = _dataRepository.GetNickname(localPlayerId.Value);
        }
        JobClass localPlayerJob = localPlayerId.HasValue
            ? _dataRepository.GetJobClass(localPlayerId.Value)
            : JobClass.Unknown;
        int targetId = 0;
        if (mode == MeterDisplayMode.Damage)
        {
            // Combat packets sometimes contain damage-like support effects on a
            // party member.  Never let such a friendly entity replace the actual
            // encounter target in the header/HP bar.  Prefer the calculator's
            // contribution target because it represents the fight being shown.
            targetId = FirstHostileTarget(
                snapshot.ContributionTargetId,
                _calculator.GetCurrentTargetId(),
                Volatile.Read(ref _selectedTargetId),
                Volatile.Read(ref _lastCombatTargetId));
        }
        Volatile.Write(ref _displayTargetId, targetId);

        double? targetHpPercent = null;
        long? targetCurrentHp = null;
        long? targetMaxHp = null;
        ActorHpData? health = targetId > 0 ? _hpTracker.GetActorHpData(targetId) : null;
        if (health is not null)
        {
            lock (health)
            {
                if (health.UpdateCount > 0 && health.MaxHp > 0)
                {
                    targetCurrentHp = health.LastHp;
                    targetMaxHp = health.MaxHp;
                    targetHpPercent = Math.Clamp(health.LastHp * 100d / health.MaxHp, 0d, 100d);
                }
            }
        }

        string healthTrace =
            $"barTarget={targetId} selected={Volatile.Read(ref _selectedTargetId)} combat={Volatile.Read(ref _lastCombatTargetId)} contribution={snapshot.ContributionTargetId} calculator={_calculator.GetCurrentTargetId()} " +
            $"hp={(targetCurrentHp?.ToString() ?? "-")}/{(targetMaxHp?.ToString() ?? "-")} pct={(targetHpPercent?.ToString("0.###") ?? "-")}";
        if (!string.Equals(healthTrace, _lastHealthTrace, StringComparison.Ordinal))
        {
            _lastHealthTrace = healthTrace;
            _diagnostics.Write("BAR", $"{healthTrace} packets={received} dropped={dropped}");
        }

        EnrageTimerView enrageTimer = mode == MeterDisplayMode.Damage
            ? BuildEnrageTimer(targetId, targetCurrentHp)
            : EnrageTimerView.Hidden;

        string targetName = mode switch
        {
            MeterDisplayMode.Healing => "Лечение группы",
            MeterDisplayMode.DamageTaken => "Полученный урон",
            _ => ResolveTargetName(targetId, snapshot.TargetName)
        };
        int targetNpcId = targetId > 0 ? _dataRepository.GetEntityNpcId(targetId) : 0;
        string? targetRole = targetNpcId > 0 ? _mobRepository.GetMobRole(targetNpcId) : null;
        targetRole ??= _mobRepository.GetMobRoleByName(targetName);
        TargetFrameKind targetFrame = mode == MeterDisplayMode.Damage
            ? ResolveTargetFrame(targetRole, targetMaxHp)
            : TargetFrameKind.Normal;

        return new MeterSnapshot(
            targetName,
            targetFrame,
            targetHpPercent,
            targetCurrentHp,
            targetMaxHp,
            snapshot.BattleTimeMs,
            snapshot.TotalDps,
            snapshot.TotalDamage,
            players,
            players.Length > 0,
            received,
            dropped,
            localPlayerId,
            localPlayerName,
            localPlayerJob,
            enrageTimer);
    }

    private uint ResolveTargetFrameBossThreshold() => _mobRepository.GetBossHpThreshold(null);

    private int FirstHostileTarget(params int[] candidates)
    {
        foreach (int candidate in candidates)
        {
            if (IsHostileTargetCandidate(candidate))
            {
                return candidate;
            }
        }

        return 0;
    }

    private bool IsHostileTargetCandidate(int entityId)
    {
        if (entityId <= 0)
        {
            return false;
        }

        // NPC evidence wins over a stale player nickname because the game reuses
        // short-lived entity ids between zones and encounters.
        if (_dataRepository.GetEntityNpcId(entityId) > 0 ||
            !string.IsNullOrWhiteSpace(_dataRepository.GetMobName(entityId)))
        {
            return true;
        }

        int? localPlayerId = _dataRepository.GetLocalPlayerEntityId()
            ?? _dataRepository.GetLastBoundLocalPlayerEntityId()
            ?? PositiveOrNull(Volatile.Read(ref _inferredLocalPlayerId));
        return localPlayerId != entityId &&
               !_dataRepository.IsConfirmedPlayer(entityId) &&
               string.IsNullOrWhiteSpace(_dataRepository.GetNickname(entityId));
    }

    private TargetFrameKind ResolveTargetFrame(string? role, long? maxHp)
    {
        if (string.Equals(role, "boss", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "miniboss", StringComparison.OrdinalIgnoreCase))
        {
            return TargetFrameKind.Legend;
        }
        if (string.Equals(role, "elite", StringComparison.OrdinalIgnoreCase))
        {
            return maxHp.GetValueOrDefault() >= ResolveTargetFrameBossThreshold()
                ? TargetFrameKind.Hero
                : TargetFrameKind.Elite;
        }

        long hp = maxHp.GetValueOrDefault();
        if (hp >= ResolveTargetFrameBossThreshold() * 4L)
        {
            return TargetFrameKind.Legend;
        }
        if (hp >= ResolveTargetFrameBossThreshold())
        {
            return TargetFrameKind.Hero;
        }
        if (hp >= 100_000)
        {
            return TargetFrameKind.Elite;
        }
        return TargetFrameKind.Normal;
    }

    private void ResolveMissingPlayerNames(MeterPlayerSnapshot[] players)
    {
        PartyRoomRoster? roster;
        lock (_featureStateLock)
        {
            roster = _partyRoster;
        }

        var usedNames = players
            .Where(player => HasUsablePlayerName(player.PlayerId, player.Nickname))
            .Select(player => player.Nickname)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        for (int index = 0; index < players.Length; index++)
        {
            MeterPlayerSnapshot player = players[index];
            if (HasUsablePlayerName(player.PlayerId, player.Nickname))
            {
                continue;
            }

            string[] candidateNames = roster?.Members
                .Where(member => member.JobClass == player.Job &&
                    !string.IsNullOrWhiteSpace(member.Name) &&
                    !usedNames.Contains(member.Name))
                .Select(member => member.Name)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToArray() ?? [];
            string? restoredName = candidateNames.Length == 1 ? candidateNames[0] : null;
            string displayName = restoredName ?? FallbackPlayerName(player.Job);
            players[index] = player with { Nickname = displayName };
            usedNames.Add(displayName);
            if (restoredName is not null)
            {
                RebindLocalPlayerFromRoster(player.PlayerId, restoredName);
            }
            if (!_identityDisplayTrace.TryGetValue(player.PlayerId, out string? previous) ||
                !string.Equals(previous, displayName, StringComparison.Ordinal))
            {
                _identityDisplayTrace[player.PlayerId] = displayName;
                _diagnostics.Write(
                    "IDENTITY",
                    $"entity={player.PlayerId} raw='{player.Nickname}' display='{displayName}' source={(restoredName is null ? "class-fallback" : "party-roster")}");
            }
        }
    }

    private void RebindLocalPlayerFromRoster(int playerId, string recoveredName)
    {
        // The party-roster match is a recovery heuristic. Once the parser has
        // observed an authoritative local-player record, never let a same-class
        // party member overwrite that live identity.
        if (Volatile.Read(ref _hasLiveLocalPlayerIdentity) != 0)
        {
            return;
        }

        string? localName = _dataRepository.GetLocalPlayerName();
        if (string.IsNullOrWhiteSpace(localName) ||
            !string.Equals(localName, recoveredName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int? previousId = _dataRepository.GetLocalPlayerEntityId()
            ?? _dataRepository.GetLastBoundLocalPlayerEntityId()
            ?? PositiveOrNull(Volatile.Read(ref _inferredLocalPlayerId));
        _dataRepository.SetNickname(
            playerId,
            recoveredName,
            isConfirmedPlayer: true,
            isAuthoritativeSource: true);
        Volatile.Write(ref _inferredLocalPlayerId, playerId);
        Volatile.Write(ref _hasLiveLocalPlayerIdentity, 1);

        if (previousId == playerId)
        {
            return;
        }

        _diagnostics.Write(
            "PLAYER",
            $"local rebound id={previousId?.ToString() ?? "-"}->{playerId} name='{recoveredName}' from party roster");
        if (_playerTargets.TryGetValue(playerId, out int targetId))
        {
            Volatile.Write(ref _selectedTargetId, targetId);
            _dataRepository.SetCurrentTarget(targetId);
            _parser.SetCurrentTarget(targetId);
        }
    }

    private static bool HasUsablePlayerName(int playerId, string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return false;
        }

        string trimmed = nickname.Trim();
        return !string.Equals(trimmed, playerId.ToString(), StringComparison.Ordinal) &&
            !string.Equals(trimmed, $"Entity #{playerId}", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(trimmed, $"Player #{playerId}", StringComparison.OrdinalIgnoreCase);
    }

    private static string FallbackPlayerName(JobClass job) => job switch
    {
        JobClass.Gladiator => "Игрок · Гладиатор",
        JobClass.Templar => "Игрок · Страж",
        JobClass.Ranger => "Игрок · Стрелок",
        JobClass.Assassin => "Игрок · Убийца",
        JobClass.Sorcerer => "Игрок · Волшебник",
        JobClass.Cleric => "Игрок · Целитель",
        JobClass.Elementalist => "Игрок · Заклинатель",
        JobClass.Chanter => "Игрок · Чародей",
        JobClass.Fighter => "Игрок · Кулачник",
        _ => "Неизвестный игрок"
    };

    private string ResolveTargetName(int targetId, string snapshotTargetName)
    {
        if (targetId > 0)
        {
            string? name = _dataRepository.GetMobName(targetId);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            int npcId = _dataRepository.GetEntityNpcId(targetId);
            name = npcId > 0 ? _mobRepository.GetMobName(npcId) : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            name = _dataRepository.GetNicknameWithLocalPlayerFallback(targetId);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshotTargetName))
        {
            return snapshotTargetName;
        }

        return _calculator.GetCurrentTargetDisplayName();
    }

    private static int? PositiveOrNull(int value) => value > 0 ? value : null;

    private EnrageTimerView BuildEnrageTimer(int targetId, long? targetCurrentHp)
    {
        if (targetId <= 0)
        {
            return EnrageTimerView.Hidden;
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int npcId = _dataRepository.GetEntityNpcId(targetId);
        if (npcId <= 0 || !_mobRepository.IsBoss(npcId))
        {
            return EnrageTimerView.Hidden;
        }

        BossPullSnapshot pull = _bossPullClock.Snapshot(targetId, nowMs);
        long currentHp = targetCurrentHp ?? 0;
        bool bossDead = targetCurrentHp.HasValue && currentHp <= 0;
        int? berserkSeconds = npcId > 0 ? _mobRepository.GetBerserkSeconds(npcId) : null;
        bool isTrial = npcId > 0 &&
            EnrageTimerPolicy.IsTrialDifficulty(_mobRepository.GetMobDifficulty(npcId));
        EnrageTimerState state = EnrageTimerPolicy.Evaluate(new EnrageTimerInput(
            berserkSeconds,
            isTrial,
            pull.PullFirstHitMs,
            nowMs,
            currentHp,
            bossDead,
            pull.PullDamage,
            pull.RecentDamage,
            pull.EnrageObservedAtMs));
        return EnrageTimerPresenter.Present(state, entitled: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _diagnostics.Write("STOP", "runtime dispose requested");
        _capture.Stop();
        _capture.Dispose();
        NpcapServiceLifecycle.TryStop(_diagnostics);
        _diagnostics.Dispose();
    }
}
