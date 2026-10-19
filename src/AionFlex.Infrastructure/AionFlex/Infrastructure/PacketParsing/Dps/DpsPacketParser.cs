using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using AionFlex.Core.Combat;
using AionFlex.Core.Diagnostics;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;
using AionFlex.Core.Schedules;
using AionFlex.Core.Servers;
using AionFlex.Core.Validation;
using AionFlex.Infrastructure.PacketParsing.Diagnostics;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing.Dps;

public sealed class DpsPacketParser : IDpsPacketParser
{
	private readonly record struct PendingStats(int GS, int CP, int Level, int? ServerId);

	private readonly record struct DecodedBundle(byte[] Data, int Length, List<int> WalkedOffsets);

	private enum LocalRecordStats
	{
		NotARecord,
		BlockMiss,
		Decoded
	}

	private readonly ILogger<DpsPacketParser> _logger;

	private readonly IDataRepository _dataRepository;

	private readonly ISkillRepository? _skillRepository;

	private readonly IPacketCaptureService? _packetCaptureService;

	private readonly GameStreamReassembler _streamReassembler;

	private int _currentTarget;

	private static readonly byte[] DamageOpcodes = new byte[2] { 4, 56 };

	private static readonly byte[] DoTOpcodes = new byte[2] { 5, 56 };

	private static readonly byte[] HpUpdateOpcodes = new byte[2] { 2, 56 };

	private static readonly byte[] TargetSelectedOpcodes = new byte[2] { 14, 56 };

	private static readonly byte[] NicknameOpcodes = new byte[2] { 4, 141 };

	private const int SwitchMask = 15;

	private const int MinValidEntityId = 100;

	private const int InsaneDamageMagnitude = 10000000;

	private const uint MaxValidEntityHp = 500000000u;

	private const int MinPlausibleCp = 1000;

	private const int MaxPlausibleCp = 3000000;

	private const double MinCpGsRatio = 15.0;

	private const double MaxCpGsRatio = 400.0;

	private int _lastCombatContextEntityId;

	private readonly ConcurrentDictionary<int, bool> _discoveredEntities = new ConcurrentDictionary<int, bool>();

	private readonly ConcurrentDictionary<int, uint> _spawnMaxHp = new ConcurrentDictionary<int, uint>();

	private readonly ConcurrentDictionary<int, uint> _framedSpawnMaxHp = new ConcurrentDictionary<int, uint>();

	private readonly ConcurrentDictionary<int, int> _framedSpawnNpcId = new ConcurrentDictionary<int, int>();

	[ThreadStatic]
	private static HashSet<int>? t_removedInCurrentPayload;

	private readonly ConcurrentDictionary<int, int> _playerServerIdCache = new ConcurrentDictionary<int, int>();

	private readonly ConcurrentDictionary<string, PendingStats> _pendingPlayerStats = new ConcurrentDictionary<string, PendingStats>();

	private readonly ConcurrentDictionary<int, (int Cp, long ObservedMs)> _pendingCpByEntity = new ConcurrentDictionary<int, (int, long)>();

	private int _pendingLocalCp;

	private long _pendingLocalCpObservedMs;

	private bool _localCpFromFramedRecord;

	private long _lastCpRejectLogTicks;

	private readonly ConcurrentDictionary<uint, string> _legionNames = new ConcurrentDictionary<uint, string>();

	private const int LegionRecordLength = 44;

	private const int MaxTrackedLegions = 512;

	private readonly Dictionary<int, (float X, float Y, float Z)> _broadcastNpcPositions = new Dictionary<int, (float, float, float)>();

	private readonly HashSet<int> _assignedBroadcastNpcIds = new HashSet<int>();

	private int _diagUniqueStreams;

	private readonly HashSet<int> _seenStreamIds = new HashSet<int>();

	private int _diagWhitelistReject;

	private readonly ConcurrentDictionary<int, long> _entityCumulativeDamage = new ConcurrentDictionary<int, long>();

	private readonly ConcurrentDictionary<int, uint> _entityFirstHp = new ConcurrentDictionary<int, uint>();

	private readonly ConcurrentDictionary<int, uint> _entityLastHp = new ConcurrentDictionary<int, uint>();

	private long _lastGapLogTicks;

	private int _diagPerfectNoMatch;

	private int _diagPerfectTotal;

	private int _diagSplitCount;

	private int _diagBrokenCount;

	private int _diag04_38Enter;

	private int _diagNonDamage;

	private int _diagSkillCodeFail;

	private int _diagTruncated;

	private int _diagSelfDamage;

	private int _diagEmitted;

	private long _diagTotalDamage;

	private int _diagStructFail;

	private int _diagNoDamageOutcome;

	private int _diagEmbeddedScannerEmit;

	private int _diagMultiHitEvents;

	private long _diagMultiHitTotalDamage;

	private int _diagDotEnter;

	private int _diagDotEmitted;

	private long _diagDotTotalDamage;

	private int _diagDotSelfDmg;

	private int _diagDotParseFail;

	private int _diagDotZeroDmg;

	private int _diagDotSkip08Reject;

	private int _diagDotWhitelistReject;

	private int _diagWideFlagsBlock;

	private int _diagUnprovenFlagsBlock;

	private int _diagDotPoolForm;

	private long _lastDiagLog;

	private readonly ConcurrentDictionary<int, int> _nonDmgSwitchVarCounts = new ConcurrentDictionary<int, int>();

	private long _lastKeepaliveTicks;

	private long _keepaliveLatencyEma;

	private bool _keepaliveLatencyInitialized;

	private long _lastLatencyEmitTicks;

	private string? _lockedDeviceName;

	private string? _currentPayloadDeviceName;

	private string _currentPayloadStreamLabel = "";

	private readonly ConcurrentDictionary<string, byte[]> _streamCarryOver = new ConcurrentDictionary<string, byte[]>();

	private const int MaxCarryOverBytes = 300;

	private long _diagCarryOverSaved;

	private long _diagCarryOverRecovered;

	private readonly FramedBundleAssembler _identityLane = new FramedBundleAssembler();

	private readonly object _identityLaneGate = new object();

	private long _laneNamed;

	private long _laneAlreadyKnown;

	private long _laneLocalSkipped;

	private long _laneDecodeRejected;

	private long _laneSubFloorSkipped;

	private long _laneSpawned;

	private long _laneSpawnAlreadyDecoded;

	private long _laneSpawnSummonSkipped;

	private long _laneSpawnRemovedSkipped;

	private long _laneSpawnDecodeRejected;

	private long _laneLocalStatsApplied;

	private long _laneLocalStatsAlreadyKnown;

	private long _laneLocalStatsNotBound;

	private long _laneLocalStatsRejected;

	private long _laneLocalBound;

	private long _laneLocalBindPreviouslyBound;

	private readonly ConcurrentDictionary<int, byte> _localEntitiesBound = new ConcurrentDictionary<int, byte>();

	private readonly PartyRoomRosterDedup _roomRosterDedup = new PartyRoomRosterDedup();

	private readonly object _roomRosterGate = new object();

	private long _roomRosterPlainRecords;

	private long _roomRosterLaneRecords;

	private long _roomRosterLaneRejected;

	private long _roomRosterWithoutLocal;

	private long _roomRosterDuplicates;

	private long _roomRosterEmitted;

	private uint _roomRosterLastLoggedRoom;

	private readonly CharacterRosterDedup _characterRosterDedup = new CharacterRosterDedup();

	private readonly object _characterRosterGate = new object();

	private readonly InventoryChangeDedup _walletChangeDedup = new InventoryChangeDedup();

	private readonly object _walletChangeGate = new object();

	private readonly TunnelFrameUnwrapper _tunnelUnwrapper;

	private readonly object _tunnelGate = new object();

	private readonly LogRateGate _tunnelAnomalyLog;

	private readonly ProtocolHealthMonitor? _protocolHealth;

	private int _bossTarget;

	[ThreadStatic]
	private static long t_segmentAgeMs;

	private Dictionary<ushort, Func<byte[], bool>>? _opcodeDispatch;

	private readonly HashSet<(int actor, int counter)> _recentMissOrHitCounters = new HashSet<(int, int)>();

	private readonly LinkedList<(int actor, int counter)> _recentMissOrHitCountersOrder = new LinkedList<(int, int)>();

	private const int MaxRecentDmgCounters = 1024;

	private readonly object _recentDmgCountersLock = new object();

	private readonly LinkedList<string> _embeddedDedupOrder = new LinkedList<string>();

	private readonly HashSet<string> _embeddedDedupSet = new HashSet<string>();

	private const int MaxEmbeddedDedupSize = 16384;

	private int _diagDedupHits;

	private readonly HashSet<(int, int, int, int)> _poolStreamKeys = new HashSet<(int, int, int, int)>();

	private readonly Queue<(int, int, int, int)> _poolStreamOrder = new Queue<(int, int, int, int)>();

	private const int MaxPoolStreamKeys = 8192;

	private int _diagDotPoolResidualDrop;

	private const long DotDedupBucketMs = 500L;

	private const long DamageEventDedupWindowMs = 3000L;

	private readonly Dictionary<(int Actor, int Target, int RawSkill, int Counter, int Damage), long> _damageEventDedup = new Dictionary<(int, int, int, int, int), long>();

	private readonly Queue<((int Actor, int Target, int RawSkill, int Counter, int Damage) Key, long Ts)> _damageEventDedupOrder = new Queue<((int, int, int, int, int), long)>();

	private const int MaxDamageEventDedupSize = 16384;

	private const long HealTickDedupWindowMs = 5000L;

	private const int MaxHealTickKeys = 4096;

	private readonly Dictionary<(int, int, int, int, int, int), long> _healTickSeenMs = new Dictionary<(int, int, int, int, int, int), long>();

	private const int PlayerInfoOneLetterNameSlot = 4;

	private const int AbyssTrailerZeroRun = 25;

	private const int AbyssTrailerTagMax = 3;

	private readonly ConcurrentDictionary<FieldBossListReject, byte> _fieldBossRejectsLogged = new ConcurrentDictionary<FieldBossListReject, byte>();

	internal const int DungeonRunStateLength = 27;

	internal const int DungeonRunResultLength = 13;

	private const int RunRecordOverlapBytes = 3;

	internal const int ServerTransferLength = 83;

	private const int SessionPrefixLogChars = 8;

	private long _lastSkillLoadoutHash;

	private static readonly byte[] SeasonJsonKeyNeedle = Encoding.ASCII.GetBytes("\"kill_count\"");

	private byte[] _seasonDecodeScratch = Array.Empty<byte>();

	private static readonly byte[] SeasonJsonAnchor = Encoding.ASCII.GetBytes("{\"kill_count\"");

	private const byte SeasonTagAbyss = 3;

	private const int MaxSeasonJsonLength = 128;

	private const long MaxSeasonRankingPoints = 1000000000L;

	private const int MaxSeasonCounter = 10000000;

	private const int MaxSeasonNameBytes = 48;

	private long _lastAbyssSeasonHash;

	private const ulong MinBoardSnapshotUnixMs = 1704067200000uL;

	private const ulong AbnormalStampMinMs = 1577836800000uL;

	private const ulong AbnormalStampMaxMs = 4102444800000uL;

	private const byte AbnormalRemoveKindPlain = 0;

	private const byte AbnormalRemoveKindBySkill = 7;

	internal bool IdentityLaneEnabled { get; set; } = Environment.GetEnvironmentVariable("DISABLE_IDENTITY_LANE") != "1";

	internal bool LaneSpawnsEnabled { get; set; } = Environment.GetEnvironmentVariable("DISABLE_LANE_SPAWNS") != "1";

	internal bool LaneLocalStatsEnabled { get; set; } = Environment.GetEnvironmentVariable("DISABLE_LANE_LOCAL_STATS") != "1";

	internal bool LaneLocalBindEnabled { get; set; } = Environment.GetEnvironmentVariable("DISABLE_LANE_LOCAL_BIND") != "1";

	internal bool TunnelUnwrapEnabled { get; set; } = Environment.GetEnvironmentVariable("DISABLE_TUNNEL_UNWRAP") != "1";

	public Func<long>? ClockOverrideMs { get; set; }

	public event EventHandler<DamageEvent>? DamageEventParsed;

	public event EventHandler<DamageEvent>? HealTickParsed;

	public event EventHandler<DefenseEvent>? DefenseEventParsed;

	public event EventHandler<AbnormalEvent>? AbnormalEventParsed;

	public event EventHandler<HpUpdateEvent>? HpUpdateEventParsed;

	public event EventHandler<GroggyGuardEvent>? GroggyGuardEventParsed;

	public event EventHandler<BossEnrageEvent>? BossEnrageDetected;

	public event EventHandler<BossSkillCastEvent>? BossSkillCastEventParsed;

	public event EventHandler<PartyRoomRosterEvent>? PartyRoomRosterParsed;

	public event EventHandler<(int PlayerId, string Nickname, int ServerId, bool IsConfirmedPlayer)>? NicknameDiscovered;

	public event EventHandler<(int PlayerId, int AbyssGrade)>? AbyssGradeDiscovered;

	public event EventHandler<AbyssSeasonRecord>? AbyssSeasonRecordDiscovered;

	public event EventHandler<(int EntityId, string Name)>? LocalPlayerDiscovered;

	public event EventHandler<int>? LocalServerIdResolved;

	public event EventHandler<(int SummonerId, int SummonId)>? SummonDetected;

	public event EventHandler<(int PlayerEntityId, int TargetEntityId)>? PlayerTargetChanged;

	#pragma warning disable CS0067 // Kept for the parser contract; this protocol revision does not emit mob names here.
	public event EventHandler<(int TargetId, string Name)>? MobNameDiscovered;
	#pragma warning restore CS0067

	public event EventHandler<(int EntityId, int TemplateCode)>? NpcTemplateDiscovered;

	public event EventHandler<(int EntityId, int NpcId, long TimestampMs, bool FromDispatch)>? NpcAppeared;

	public event EventHandler<(int EntityId, int OwnerId, int NpcId, long TimestampMs)>? SummonAppeared;

	public event EventHandler<(int EntityId, int Reason, long TimestampMs)>? EntityRemoved;

	public event EventHandler<(int BaseSkillCode, HashSet<int> SpecIndices, int SkillLevel)>? SkillSpecsDetected;

	public event EventHandler<SkillLoadoutSnapshot>? SkillLoadoutDecoded;

	public event EventHandler<int>? ZoneChanged;

	public event EventHandler<(int EntityId, float X, float Y, long TimestampMs)>? LocalPositionParsed;

	public event EventHandler<ServerTransferEvent>? ServerTransferred;

	public event EventHandler<DungeonRunStateEvent>? DungeonRunStateChanged;

	public event EventHandler<DungeonRunResultEvent>? DungeonRunResultReceived;

	public event EventHandler<LootEnergyEvent>? LootEnergyUpdated;

	public event EventHandler<ContentsTicketsEvent>? ContentsTicketsUpdated;

	public event EventHandler<CharacterRosterEvent>? CharacterRosterReceived;

	public event EventHandler<WalletEvent>? WalletUpdated;

	public event EventHandler<FieldBossListEvent>? FieldBossListParsed;

	public event EventHandler<string>? GameServerDiscovered;

	public event EventHandler<long>? LatencyMeasured;

	[Conditional("DEV_BUILD")]
	private static void DiagBump(int actor, int skill, string reason)
	{
	}

	[Conditional("DEV_BUILD")]
	private static void DiagHitQuality(int actor, int target, int skill, int rawSkill, int damage, int type, int flag, int switchVar, int loop, int unknown, int counter, int multiHitCount, ReadOnlySpan<byte> flagsBlock, long ts)
	{
	}

	private void LockGameStream()
	{
		if (_lockedDeviceName == null && _currentPayloadDeviceName != null)
		{
			_lockedDeviceName = _currentPayloadDeviceName;
			_logger.LogInformation("\ud83d\udd12 Game stream locked to device: {Device}", _lockedDeviceName);
		}
	}

	public DpsPacketParser(ILogger<DpsPacketParser> logger, IDataRepository dataRepository, ISkillRepository? skillRepository = null, IPacketCaptureService? packetCaptureService = null, ProtocolHealthMonitor? protocolHealth = null)
	{
		_logger = logger ?? throw new ArgumentNullException("logger");
		_dataRepository = dataRepository ?? throw new ArgumentNullException("dataRepository");
		_skillRepository = skillRepository;
		_packetCaptureService = packetCaptureService;
		_protocolHealth = protocolHealth;
		_streamReassembler = new GameStreamReassembler(logger);
		_streamReassembler.PacketReady += OnPacketReceived;
		Func<long> clockMs = TunnelClockMs;
		_tunnelAnomalyLog = new LogRateGate(null, 256, null, clockMs);
		_tunnelUnwrapper = new TunnelFrameUnwrapper
		{
			AnomalyObserved = OnTunnelAnomaly
		};
	}

	public void SetCurrentTarget(int targetId)
	{
		_currentTarget = targetId;
		_bossTarget = targetId;
	}

	public void ClearDiscoveredEntities()
	{
		_discoveredEntities.Clear();
		_broadcastNpcPositions.Clear();
		_assignedBroadcastNpcIds.Clear();
		long num = _packetCaptureService?.TcpRetransmissions ?? 0;
		long num2 = _packetCaptureService?.TcpGaps ?? 0;
		long num3 = _packetCaptureService?.TcpGapBytes ?? 0;
		_logger.LogDebug("\ud83d\udcca DAMAGE DIAG (final): Enter={Enter}, NonDmg={NonDmg}, SkillFail={SkillFail}, StructFail={StructFail}, Trunc={Trunc}, Self={Self}, WL_Reject={WLReject}, Emitted={Emitted}, TotalDmg={TotalDmg:N0}, Streams={Streams} | Routing: Perfect={Perfect}, Split={Split}, Broken={Broken}, NoMatch={NoMatch} | EmbeddedScanner={EmbScanner} | Stream: Reassembled={Reasm}, Direct={Direct}, Skipped={Skip}, Stalls={Stall}, Pending={Pending} | MultiHit: Events={MHEvents}, Dmg={MHDmg:N0} | DoT: Enter={DotEnter}, Emitted={DotEmit}, TotalDmg={DotDmg:N0} | TcpRetrans={TcpRetrans}, TcpGaps={TcpGaps}, GapBytes={GapBytes} | FlagsBlock: Wide={WideFB}, Unproven={UnprovenFB}", _diag04_38Enter, _diagNonDamage, _diagSkillCodeFail, _diagStructFail, _diagTruncated, _diagSelfDamage, _diagWhitelistReject, _diagEmitted, _diagTotalDamage, _diagUniqueStreams, _diagPerfectTotal, _diagSplitCount, _diagBrokenCount, _diagPerfectNoMatch, _diagEmbeddedScannerEmit, _streamReassembler.DiagReassembled, _streamReassembler.DiagDirect, _streamReassembler.DiagSkipped, _streamReassembler.DiagStallReset, _streamReassembler.PendingBytes, _diagMultiHitEvents, _diagMultiHitTotalDamage, _diagDotEnter, _diagDotEmitted, _diagDotTotalDamage, num, num2, num3, _diagWideFlagsBlock, _diagUnprovenFlagsBlock);
		_diag04_38Enter = (_diagNonDamage = (_diagSkillCodeFail = (_diagStructFail = (_diagTruncated = (_diagSelfDamage = (_diagEmitted = (_diagDedupHits = 0)))))));
		_diagTotalDamage = 0L;
		_diagMultiHitEvents = 0;
		_diagMultiHitTotalDamage = 0L;
		_diagWhitelistReject = 0;
		_diagEmbeddedScannerEmit = 0;
		_diagPerfectNoMatch = 0;
		_diagPerfectTotal = 0;
		_diagSplitCount = 0;
		_diagBrokenCount = 0;
		_seenStreamIds.Clear();
		_diagUniqueStreams = 0;
		_diagDotEnter = (_diagDotEmitted = (_diagDotSelfDmg = (_diagDotParseFail = (_diagDotZeroDmg = (_diagDotSkip08Reject = (_diagDotWhitelistReject = (_diagDotPoolForm = (_diagDotPoolResidualDrop = 0))))))));
		_diagDotTotalDamage = 0L;
		_lastDiagLog = 0L;
		_streamCarryOver.Clear();
		_diagCarryOverSaved = 0L;
		_diagCarryOverRecovered = 0L;
		_streamReassembler.ResetDiagnostics();
		_streamReassembler.Reset();
		_entityCumulativeDamage.Clear();
		_entityFirstHp.Clear();
		_entityLastHp.Clear();
		_poolStreamKeys.Clear();
		_poolStreamOrder.Clear();
		_lastGapLogTicks = 0L;
		_logger.LogInformation("Cleared discovered entities cache and HP tracking state");
	}

	public void ResetEntityLife(int entityId)
	{
		_spawnMaxHp.TryRemove(entityId, out var value);
		_framedSpawnMaxHp.TryRemove(entityId, out value);
		_framedSpawnNpcId.TryRemove(entityId, out var _);
		_entityCumulativeDamage.TryRemove(entityId, out var _);
		_entityFirstHp.TryRemove(entityId, out value);
		_entityLastHp.TryRemove(entityId, out value);
	}

	public void ProcessPayload(CapturedPayload payload)
	{
		if (payload?.Data == null || payload.Data.Length == 0 || (_lockedDeviceName != null && payload.DeviceName != null && payload.DeviceName != _lockedDeviceName))
		{
			return;
		}
		List<TunnelFrameUnwrapper.GameSegment> segments = null;
		long nowMs = 0L;
		bool flag = TunnelUnwrapEnabled && TryUnwrapTunnelPayload(payload, out segments, out nowMs);
		if (segments != null)
		{
			foreach (TunnelFrameUnwrapper.GameSegment item in segments)
			{
				string text = item.Device ?? payload.DeviceName;
				if (_lockedDeviceName == null || text == null || !(text != _lockedDeviceName))
				{
					ProcessHeldGamePayload(new CapturedPayload(item.Src, item.Dst, item.Data, text), nowMs - item.ArrivedMs);
				}
			}
		}
		if (!flag)
		{
			ProcessGamePayload(payload);
		}
	}

	private void ProcessHeldGamePayload(CapturedPayload payload, long ageMs)
	{
		if (ageMs <= 0)
		{
			ProcessGamePayload(payload);
			return;
		}
		long num = t_segmentAgeMs;
		t_segmentAgeMs = ageMs;
		try
		{
			ProcessGamePayload(payload);
		}
		finally
		{
			t_segmentAgeMs = num;
		}
	}

	private long TunnelClockMs()
	{
		return ClockOverrideMs?.Invoke() ?? Environment.TickCount64;
	}

	private bool TryUnwrapTunnelPayload(CapturedPayload payload, out List<TunnelFrameUnwrapper.GameSegment>? segments, out long nowMs)
	{
		segments = null;
		nowMs = TunnelClockMs();
		long streamsDetected;
		bool flag;
		try
		{
			lock (_tunnelGate)
			{
				streamsDetected = _tunnelUnwrapper.StreamsDetected;
				flag = _tunnelUnwrapper.Append(new TunnelFrameUnwrapper.StreamKey(payload.DeviceName, payload.SourcePort, payload.DestinationPort), payload.Data, nowMs, ref segments);
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Tunnel unwrap error on {Src}→{Dst}", payload.SourcePort, payload.DestinationPort);
			return false;
		}
		if (flag && streamsDetected == 0L)
		{
			_logger.LogInformation("\ud83e\uddc5 Accelerator tunnel framing on {Device} {Src}→{Dst}: its game segments are unwrapped before parsing", payload.DeviceName, payload.SourcePort, payload.DestinationPort);
		}
		if (segments != null)
		{
			foreach (TunnelFrameUnwrapper.GameSegment segment in segments)
			{
				if (segment.FirstOfConnection)
				{
					_logger.LogDebug("\ud83e\uddc5 Tunnel connection {Src}→{Dst}: game segments unwrapped", segment.Src, segment.Dst);
				}
			}
		}
		return flag;
	}

	private void OnTunnelAnomaly(TunnelFrameUnwrapper.StreamKey stream, TunnelFrameUnwrapper.Anomaly anomaly, int detail)
	{
		if (_tunnelAnomalyLog.ShouldLog($"{anomaly}|{stream.Device}|{stream.Src}>{stream.Dst}", out var repeats))
		{
			string text = _tunnelAnomalyLog.SummarySuffix(repeats);
			switch (anomaly)
			{
			case TunnelFrameUnwrapper.Anomaly.BadFrame:
				_logger.LogWarning("\ud83e\uddc5 Tunnel stream {Device} {Src}→{Dst}: a {Bytes}-byte frame does not walk — a TCP gap on this leg (the other leg carries the frames), or the tunnel format changed if this repeats; resyncing at the next frame start{Repeats}", stream.Device, stream.Src, stream.Dst, detail, text);
				break;
			case TunnelFrameUnwrapper.Anomaly.LostAfterSkip:
				_logger.LogWarning("\ud83e\uddc5 Tunnel stream {Device} {Src}→{Dst}: a game segment arrived {LateMs} ms after its gap was given up — it is NOT parsed (a leg lagged past the reorder hold, or the hold hit its memory cap){Repeats}", stream.Device, stream.Src, stream.Dst, detail, text);
				break;
			case TunnelFrameUnwrapper.Anomaly.MissingIdentity:
				_logger.LogWarning("\ud83e\uddc5 Tunnel stream {Device} {Src}→{Dst}: a data frame has no connection id / direction / sequence — its game bytes are NOT parsed (tunnel format changed?){Repeats}", stream.Device, stream.Src, stream.Dst, text);
				break;
			default:
				_logger.LogWarning("\ud83e\uddc5 Tunnel stream {Device} {Src}→{Dst}: frame kind {Kind:x2} is not a known tunnel frame, or its game bytes are outside a data frame — nothing in it is parsed (tunnel format changed?){Repeats}", stream.Device, stream.Src, stream.Dst, detail, text);
				break;
			}
		}
	}

	public TunnelUnwrapStats GetTunnelUnwrapStats()
	{
		lock (_tunnelGate)
		{
			TunnelFrameUnwrapper tunnelUnwrapper = _tunnelUnwrapper;
			return new TunnelUnwrapStats(tunnelUnwrapper.StreamsDetected, tunnelUnwrapper.TunnelPayloads, tunnelUnwrapper.DataFrames, tunnelUnwrapper.Delivered, tunnelUnwrapper.Duplicates, tunnelUnwrapper.ControlFrames, tunnelUnwrapper.BadFrames, tunnelUnwrapper.MissingIdentity, tunnelUnwrapper.UnrecognisedFrames, tunnelUnwrapper.Desyncs, tunnelUnwrapper.Resyncs, tunnelUnwrapper.ResyncSkippedBytes, tunnelUnwrapper.HeldFrames, tunnelUnwrapper.LateFills, tunnelUnwrapper.GapsSkipped, tunnelUnwrapper.SkippedSequences, tunnelUnwrapper.LostAfterSkip, tunnelUnwrapper.MaxHoldMs, tunnelUnwrapper.MaxHeldBytesSeen, tunnelUnwrapper.HeldBytesNow, tunnelUnwrapper.StreamsEvicted, tunnelUnwrapper.ConnectionsSeen, tunnelUnwrapper.ConnectionsEvicted, tunnelUnwrapper.MaxRetainedBytes, tunnelUnwrapper.Errors);
		}
	}

	private void ProcessGamePayload(CapturedPayload payload)
	{
		_ = Environment.TickCount64;
		t_removedInCurrentPayload?.Clear();
		try
		{
			_currentPayloadDeviceName = payload.DeviceName;
			_currentPayloadStreamLabel = $"{payload.SourcePort}→{payload.DestinationPort}";
			byte[] array = payload.Data;
			if (_streamCarryOver.TryRemove(_currentPayloadStreamLabel, out byte[] value) && value.Length != 0)
			{
				byte[] array2 = new byte[value.Length + array.Length];
				Buffer.BlockCopy(value, 0, array2, 0, value.Length);
				Buffer.BlockCopy(array, 0, array2, value.Length, array.Length);
				array = array2;
				Interlocked.Increment(ref _diagCarryOverRecovered);
			}
			ScanForPlayerStats(array);
			ScanForCombatPower(array);
			ScanForRosterCp(array);
			ScanForRoomRosters(array);
			ScanForLegionRecord(array);
			ScanForServerClock(array);
			TryScanLocalPlayerRecord(array);
			if (Environment.GetEnvironmentVariable("DISABLE_UNFRAMED_BUNDLES") != "1")
			{
				ScanForUnframedBundles(array);
			}
			List<(int, int)> compressedExtents = FindDecodableBundleExtents(array);
			if (Environment.GetEnvironmentVariable("DISABLE_BUNDLE_BRUTAL") != "1")
			{
				ScanDecompressedBundleBrutal(array, array.Length, compressedExtents);
			}
			int num = ProcessPacketStream(array);
			bool flag = false;
			if (num >= 0 && num < array.Length)
			{
				int num2 = array.Length - num;
				if (num2 > 0 && num2 <= 300)
				{
					_streamCarryOver[_currentPayloadStreamLabel] = array[num..];
					Interlocked.Increment(ref _diagCarryOverSaved);
					flag = true;
				}
			}
			if (!flag && num >= 0 && num < array.Length)
			{
				ScanForEmbeddedDamage(array[num..], num, compressedExtents);
			}
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "Error processing payload");
		}
		if (IdentityLaneEnabled)
		{
			RunIdentityLane(payload);
		}
	}

	private void RunIdentityLane(CapturedPayload payload)
	{
		List<byte[]> playerInfoRecords = null;
		List<byte[]> spawnRecords = null;
		List<byte[]> localRecords = null;
		List<byte[]> roomRosterRecords = null;
		List<byte[]> ticketListRecords = null;
		List<byte[]> fieldBossListRecords = null;
		List<byte[]> characterRosterRecords = null;
		List<byte[]> inventoryRecords = null;
		try
		{
			int? lastBoundLocalPlayerEntityId = _dataRepository.GetLastBoundLocalPlayerEntityId();
			if (lastBoundLocalPlayerEntityId.HasValue)
			{
				int valueOrDefault = lastBoundLocalPlayerEntityId.GetValueOrDefault();
				_localEntitiesBound.TryAdd(valueOrDefault, 0);
			}
			lock (_identityLaneGate)
			{
				_identityLane.Append(new FramedBundleAssembler.StreamKey(payload.DeviceName, payload.SourcePort, payload.DestinationPort), payload.Data, ref playerInfoRecords, ref spawnRecords, ref localRecords, ref roomRosterRecords, ref ticketListRecords, ref fieldBossListRecords, ref characterRosterRecords, ref inventoryRecords);
			}
			if (playerInfoRecords == null && spawnRecords == null && localRecords == null && roomRosterRecords == null && ticketListRecords == null && fieldBossListRecords == null && characterRosterRecords == null && inventoryRecords == null)
			{
				return;
			}
			if (playerInfoRecords != null)
			{
				foreach (byte[] item in playerInfoRecords)
				{
					TryApplyLanePlayerInfo(item, payload.SourcePort, payload.DestinationPort);
				}
			}
			if (spawnRecords != null && LaneSpawnsEnabled)
			{
				foreach (byte[] item2 in spawnRecords)
				{
					TryApplyLaneNpcSpawn(item2, payload.SourcePort, payload.DestinationPort);
				}
			}
			if (localRecords != null && LaneLocalBindEnabled)
			{
				foreach (byte[] item3 in localRecords)
				{
					TryApplyLaneLocalBind(item3, payload.SourcePort, payload.DestinationPort);
				}
			}
			if (localRecords != null && LaneLocalStatsEnabled)
			{
				foreach (byte[] item4 in localRecords)
				{
					TryApplyLaneLocalStats(item4, payload.SourcePort, payload.DestinationPort);
				}
			}
			if (roomRosterRecords != null)
			{
				foreach (byte[] item5 in roomRosterRecords)
				{
					HandleRoomRosterRecord(item5);
				}
			}
			if (ticketListRecords != null)
			{
				foreach (byte[] item6 in ticketListRecords)
				{
					if (LootEnergyRecordDecoder.TryDecodeList(item6, out var reading))
					{
						LootEnergyUpdated?.Invoke(this, new LootEnergyEvent(reading.Daily, reading.Bonus, FromLoginList: true, NowMs()));
					}
					if (LootEnergyRecordDecoder.TryDecodeListEntries(item6, out IReadOnlyList<ContentsTicketEntry> entries))
					{
						ContentsTicketsUpdated?.Invoke(this, new ContentsTicketsEvent(entries, FromLoginList: true, NowMs()));
					}
				}
			}
			if (fieldBossListRecords != null)
			{
				foreach (byte[] item7 in fieldBossListRecords)
				{
					if (!TryRaiseFieldBossList(item7, out var reject))
					{
						LogFieldBossListReject(reject, "identity lane", item7);
					}
				}
			}
			if (characterRosterRecords != null)
			{
				foreach (byte[] item8 in characterRosterRecords)
				{
					TryEmitCharacterRoster(item8, "identity lane");
				}
			}
			if (inventoryRecords == null)
			{
				return;
			}
			foreach (byte[] item9 in inventoryRecords)
			{
				if (!TryEmitWalletList(item9, "identity lane"))
				{
					TryEmitWalletChange(item9, "identity lane");
				}
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Identity lane error");
		}
	}

	private void TryApplyLaneNpcSpawn(byte[] record, int sourcePort, int destinationPort)
	{
		if (!TryReadNpcSpawnHeader(record, out var entityId, out var flag, out var _, out var npcId, out var _))
		{
			Interlocked.Increment(ref _laneSpawnDecodeRejected);
			return;
		}
		if ((flag & 0x1F) == 31)
		{
			Interlocked.Increment(ref _laneSpawnSummonSkipped);
			return;
		}
		if (_framedSpawnNpcId.TryGetValue(entityId, out var value) && value == npcId)
		{
			Interlocked.Increment(ref _laneSpawnAlreadyDecoded);
			return;
		}
		HashSet<int>? hashSet = t_removedInCurrentPayload;
		if (hashSet != null && hashSet.Contains(entityId))
		{
			Interlocked.Increment(ref _laneSpawnRemovedSkipped);
			return;
		}
		if (!TryParseNpcSpawnPacket(record, fromDispatch: true))
		{
			Interlocked.Increment(ref _laneSpawnDecodeRejected);
			return;
		}
		Interlocked.Increment(ref _laneSpawned);
		_logger.LogInformation("\ud83e\udeaa Identity lane: NPC spawn {Entity} = npc {NpcId} ({Src}→{Dst}) at {AtMs}", entityId, npcId, sourcePort, destinationPort, NowMs());
	}

	private void TryApplyLaneLocalBind(byte[] record, int sourcePort, int destinationPort)
	{
		int item = ReadVarIntStatic(record, 0).Length;
		if (item <= 0 || TryReadLocalRecordStats(record, item, out int entityId, out string name, out int serverId, out int _, out int _, out int _) == LocalRecordStats.NotARecord)
		{
			return;
		}
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		if (string.IsNullOrEmpty(localPlayerName) || !string.Equals(localPlayerName, name, StringComparison.Ordinal) || _dataRepository.GetCurrentServerId() != serverId || _dataRepository.GetLocalPlayerEntityId() == entityId)
		{
			return;
		}
		if (_localEntitiesBound.ContainsKey(entityId))
		{
			Interlocked.Increment(ref _laneLocalBindPreviouslyBound);
			return;
		}
		_logger.LogInformation("\ud83e\udeaa Identity lane: local player re-bound to {Entity} '{Name}' (zone-in record the router could not read, {Src}→{Dst}) at {AtMs}", entityId, name, sourcePort, destinationPort, NowMs());
		TryScanLocalPlayerRecord(record);
		if (_dataRepository.GetLocalPlayerEntityId() == entityId)
		{
			_localEntitiesBound.TryAdd(entityId, 0);
			Interlocked.Increment(ref _laneLocalBound);
		}
	}

	private void TryApplyLaneLocalStats(byte[] record, int sourcePort, int destinationPort)
	{
		int item = ReadVarIntStatic(record, 0).Length;
		if (item <= 0 || TryReadLocalRecordStats(record, item, out int entityId, out string name, out int serverId, out int _, out int level, out int gs) != LocalRecordStats.Decoded)
		{
			Interlocked.Increment(ref _laneLocalStatsRejected);
			return;
		}
		if (_dataRepository.GetLocalPlayerEntityId() != entityId || !string.Equals(_dataRepository.GetLocalPlayerName(), name, StringComparison.Ordinal) || _dataRepository.GetCurrentServerId() != serverId)
		{
			Interlocked.Increment(ref _laneLocalStatsNotBound);
			return;
		}
		var (num, _, num2) = _dataRepository.GetPlayerStats(entityId);
		if (num == gs && (level == 0 || num2 == level))
		{
			Interlocked.Increment(ref _laneLocalStatsAlreadyKnown);
			return;
		}
		ApplyStats(name, gs, level, serverId);
		Interlocked.Increment(ref _laneLocalStatsApplied);
		_logger.LogInformation("\ud83e\udeaa Identity lane: local {Entity} '{Name}' Level={Level} GS={GS} ({Src}→{Dst}) at {AtMs}", entityId, name, level, gs, sourcePort, destinationPort, NowMs());
	}

	private void TryApplyLanePlayerInfo(byte[] record, int sourcePort, int destinationPort)
	{
		if (!TryDecodePlayerInfo(record, out int entityId, out string name))
		{
			Interlocked.Increment(ref _laneDecodeRejected);
			return;
		}
		if (entityId < 100)
		{
			Interlocked.Increment(ref _laneSubFloorSkipped);
			return;
		}
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		if (localPlayerName != null && string.Equals(localPlayerName, name, StringComparison.Ordinal))
		{
			Interlocked.Increment(ref _laneLocalSkipped);
			return;
		}
		if (_dataRepository.IsConfirmedPlayer(entityId) && string.Equals(_dataRepository.GetNickname(entityId), name, StringComparison.Ordinal))
		{
			Interlocked.Increment(ref _laneAlreadyKnown);
			return;
		}
		_dataRepository.SetNickname(entityId, name, isConfirmedPlayer: true);
		NicknameDiscovered?.Invoke(this, (entityId, name, 0, true));
		ApplyPendingPlayerStats(entityId, name);
		if (TryReadAbyssGrade(record, out var grade))
		{
			AbyssGradeDiscovered?.Invoke(this, (entityId, grade));
		}
		Interlocked.Increment(ref _laneNamed);
		_logger.LogInformation("\ud83e\udeaa Identity lane: {Entity} = '{Name}' ({Src}→{Dst}) at {AtMs}", entityId, name, sourcePort, destinationPort, NowMs());
	}

	public IdentityLaneStats GetIdentityLaneStats()
	{
		lock (_identityLaneGate)
		{
			FramedBundleAssembler identityLane = _identityLane;
			return new IdentityLaneStats(identityLane.Candidates, identityLane.RatioRejected, identityLane.OverCap, identityLane.DecodeAttempts, identityLane.Decoded, identityLane.DecodeFailed, identityLane.PlayerInfoRecords, identityLane.NestedSkipped, identityLane.StreamsEvicted, identityLane.EvictedWithPending, identityLane.Errors, identityLane.MaxRetainedBytes, Interlocked.Read(in _laneNamed), Interlocked.Read(in _laneAlreadyKnown), Interlocked.Read(in _laneLocalSkipped), Interlocked.Read(in _laneDecodeRejected), Interlocked.Read(in _laneSubFloorSkipped), identityLane.PendingRefused, identityLane.MaxPendingSeen, identityLane.SpawnRecords, Interlocked.Read(in _laneSpawned), Interlocked.Read(in _laneSpawnAlreadyDecoded), Interlocked.Read(in _laneSpawnSummonSkipped), Interlocked.Read(in _laneSpawnRemovedSkipped), Interlocked.Read(in _laneSpawnDecodeRejected), identityLane.LocalRecords, Interlocked.Read(in _laneLocalStatsApplied), Interlocked.Read(in _laneLocalStatsAlreadyKnown), Interlocked.Read(in _laneLocalStatsNotBound), Interlocked.Read(in _laneLocalStatsRejected), Interlocked.Read(in _laneLocalBound), Interlocked.Read(in _laneLocalBindPreviouslyBound));
		}
	}

	private int ProcessPacketStream(byte[] data)
	{
		int num = 0;
		while (num < data.Length - 2)
		{
			var (num2, num3) = ReadVarInt(data, num);
			if (num3 < 0)
			{
				num++;
				continue;
			}
			if (num2 == 6 && num + num3 + 1 < data.Length && data[num + num3] == 0 && data[num + num3 + 1] == 54)
			{
				ProcessKeepalive();
				num += num3 + 2;
				continue;
			}
			if (num2 <= 3)
			{
				num++;
				continue;
			}
			int num4 = data.Length - num;
			if (num4 == num2)
			{
				_diagPerfectTotal++;
				ParsePerfectPacket(data[num..]);
				return -1;
			}
			if (num2 > num4)
			{
				_diagBrokenCount++;
				ParseBrokenLengthPacket(data[num..]);
				return num;
			}
			_diagSplitCount++;
			byte[] subArray = data[num..(num + num2)];
			if (subArray.Length > 3)
			{
				_diagPerfectTotal++;
				ParsePerfectPacket(subArray);
			}
			num += num2 - 3;
		}
		return -1;
	}

	private void ProcessKeepalive()
	{
		long tickCount = Environment.TickCount64;
		if (_lastKeepaliveTicks > 0)
		{
			long num = tickCount - _lastKeepaliveTicks;
			long num2 = Math.Max(0L, num - 500);
			if (!_keepaliveLatencyInitialized)
			{
				_keepaliveLatencyEma = num2;
				_keepaliveLatencyInitialized = true;
			}
			else
			{
				_keepaliveLatencyEma = (_keepaliveLatencyEma * 70 + num2 * 30) / 100;
			}
			if (tickCount - _lastLatencyEmitTicks > 3000)
			{
				_lastLatencyEmitTicks = tickCount;
				LatencyMeasured?.Invoke(this, _keepaliveLatencyEma);
			}
		}
		_lastKeepaliveTicks = tickCount;
	}

	private void OnPacketReceived(byte[] packet)
	{
		if (packet.Length < 3)
		{
			return;
		}
		if (packet.Length > 18)
		{
			ScanForPlayerStats(packet);
			ScanForCombatPower(packet);
			ScanForRosterCp(packet);
			ScanForLegionRecord(packet);
		}
		var (num, num2) = ReadVarInt(packet);
		if (num2 < 0)
		{
			return;
		}
		if (num == 6 && num2 + 1 < packet.Length && packet[num2] == 0 && packet[num2 + 1] == 54)
		{
			long tickCount = Environment.TickCount64;
			if (_lastKeepaliveTicks > 0)
			{
				long num3 = tickCount - _lastKeepaliveTicks;
				long num4 = Math.Max(0L, num3 - 500);
				if (!_keepaliveLatencyInitialized)
				{
					_keepaliveLatencyEma = num4;
					_keepaliveLatencyInitialized = true;
				}
				else
				{
					_keepaliveLatencyEma = (_keepaliveLatencyEma * 70 + num4 * 30) / 100;
				}
				if (tickCount - _lastLatencyEmitTicks > 3000)
				{
					_lastLatencyEmitTicks = tickCount;
					LatencyMeasured?.Invoke(this, _keepaliveLatencyEma);
				}
			}
			_lastKeepaliveTicks = tickCount;
			int num5 = num2 + 2;
			if (num5 < packet.Length)
			{
				OnPacketReceived(packet[num5..]);
			}
		}
		else
		{
			if (packet.Length <= 3)
			{
				return;
			}
			if (_logger.IsEnabled(LogLevel.Trace) && packet.Length >= 3)
			{
				string text = BitConverter.ToString((packet.Length > 50) ? packet[..50] : packet).Replace("-", " ");
				_logger.LogTrace("\ud83d\udce6 RAW PACKET ({0} bytes): {1}{2}", packet.Length, text, (packet.Length > 50) ? "..." : "");
			}
			if (packet.Length == num)
			{
				_logger.LogTrace("Perfect packet: {Length} bytes", num);
				_diagPerfectTotal++;
				ParsePerfectPacket(packet);
				return;
			}
			if (num > packet.Length)
			{
				_diagBrokenCount++;
				ParseBrokenLengthPacket(packet);
				return;
			}
			if (num > 3)
			{
				try
				{
					_diagSplitCount++;
					byte[] subArray = packet[..num];
					if (subArray.Length > 3)
					{
						_logger.LogTrace("Packet split succeeded: {Length} bytes", subArray.Length);
						_diagPerfectTotal++;
						ParsePerfectPacket(subArray);
					}
					if (num - 3 < packet.Length)
					{
						OnPacketReceived(packet[(num - 3)..]);
					}
					return;
				}
				catch (Exception exception)
				{
					_logger.LogError(exception, "Exception while splitting packet");
					return;
				}
			}
			OnPacketReceived(packet[1..]);
		}
	}

	private Dictionary<ushort, Func<byte[], bool>> BuildOpcodeDispatch()
	{
		return new Dictionary<ushort, Func<byte[], bool>>
		{
			{ 1080, TryParseDamagePacket },
			{ 1336, TryParseDoTPacket },
			{ 568, TryParseActionPacket },
			{ 141, TryParseHpUpdatePacket },
			{ 1165, TryParseNicknamePacket },
			{ 3640, TryParseTargetSelectedPacket },
			{ 16438, TryParse4036Packet },
			{ 16694, TryParse4036Packet },
			{ 16950, TryParse4236Packet },
			{ 15672, TryParseBatchContainer },
			{ 13624, TryParseSummonSpawnPacket },
			{ 824, TryParseNpcPositionPacket },
			{ 6998, TryParse1B56Packet },
			{ 5430, TryParseServerTransferPacket },
			{ 9782, TryParseCharacterRosterPacket },
			{ 226, TryParseServerClockOffsetPacket },
			{ 97, TryParseDungeonRunStatePacket },
			{ 353, TryParseDungeonRunResultPacket },
			{ 3169, TryParseLootEnergyChangePacket },
			{ 5686, TryParseGameServerPacket },
			{ 401, TryParse0191Packet },
			{ 17462, TryParsePlayerInfoPacket },
			{ 17718, TryParsePlayerInfoPacket },
			{ 1592, TryParseDefenseEffectPacket },
			{ 10808, TryParseAbnormalApplyPacket },
			{ 11064, TryParseAbnormalApplyPacket },
			{ 11320, TryParseAbnormalRemovePacket },
			{ 1504, TryParseGroggyGuardPacket },
			{ 2360, TryParseSkillCastPacket },
			{ 8502, TryParseZonePacket },
			{ 15415, TryParseLocalPositionPacket }
		};
	}

	private bool TryParseZonePacket(byte[] packet)
	{
		int frame = ReadVarInt(packet).Length;
		if (frame <= 0 || frame + 10 > packet.Length || packet[frame] != 0x21 || packet[frame + 1] != 0x36)
		{
			return false;
		}

		int zoneId = BitConverter.ToInt32(packet, frame + 6);
		if (zoneId <= 0)
		{
			return false;
		}

		ZoneChanged?.Invoke(this, zoneId);
		if (frame + 26 <= packet.Length)
		{
			float x = BitConverter.ToSingle(packet, frame + 18);
			float y = BitConverter.ToSingle(packet, frame + 22);
			if (float.IsFinite(x) && float.IsFinite(y) && Math.Abs(x) < 1_000_000 && Math.Abs(y) < 1_000_000)
			{
				int entityId = _dataRepository.GetLocalPlayerEntityId() ?? 0;
				LocalPositionParsed?.Invoke(this, (entityId, x, y, NowMs()));
			}
		}

		return true;
	}

	private bool TryParseLocalPositionPacket(byte[] packet)
	{
		int frame = ReadVarInt(packet).Length;
		if (frame <= 0 || frame + 3 >= packet.Length || packet[frame] != 0x3c || packet[frame + 1] != 0x37)
		{
			return false;
		}

		int cursor = frame + 2;
		(int entityId, int entityLength) = ReadVarInt(packet, cursor);
		if (entityLength <= 0)
		{
			return false;
		}

		cursor += entityLength;
		if (cursor + 9 > packet.Length || packet[cursor] != 3)
		{
			return false;
		}

		int? localId = _dataRepository.GetLocalPlayerEntityId() ?? _dataRepository.GetLastBoundLocalPlayerEntityId();
		if (localId.HasValue && localId.Value != entityId)
		{
			return true;
		}

		float x = BitConverter.ToSingle(packet, cursor + 1);
		float y = BitConverter.ToSingle(packet, cursor + 5);
		if (float.IsFinite(x) && float.IsFinite(y) && Math.Abs(x) < 1_000_000 && Math.Abs(y) < 1_000_000)
		{
			LocalPositionParsed?.Invoke(this, (entityId, x, y, NowMs()));
		}

		return true;
	}

	private bool TryParse4036Packet(byte[] packet)
	{
		if (TryParseSummonPacket(packet, fromDispatch: true))
		{
			return true;
		}
		return TryParseNpcSpawnPacket(packet, fromDispatch: true);
	}

	private bool TryParse4236Packet(byte[] packet)
	{
		if (!TryParseEntityRemovalPacket(packet))
		{
			return TryParse4036Packet(packet);
		}
		return true;
	}

	private bool TryParseEntityRemovalPacket(byte[] packet)
	{
		var (num, num2) = ReadVarInt(packet);
		if (num2 <= 0)
		{
			return false;
		}
		int num3 = num2;
		if (num3 + 1 >= packet.Length || packet[num3] != 66 || packet[num3 + 1] != 54)
		{
			return false;
		}
		num3 += 2;
		var (num4, num5) = ReadVarInt(packet, num3);
		if (num5 <= 0 || num4 <= 0)
		{
			return false;
		}
		num3 += num5;
		if (num != num3 + 2 + 3)
		{
			return false;
		}
		if (num3 + 1 >= packet.Length || packet[num3] != 0)
		{
			return false;
		}
		(t_removedInCurrentPayload ?? (t_removedInCurrentPayload = new HashSet<int>())).Add(num4);
		EntityRemoved?.Invoke(this, (num4, packet[num3 + 1], NowMs()));
		return true;
	}

	private void ParsePerfectPacket(byte[] packet)
	{
		if (packet.Length < 3)
		{
			return;
		}
		int item = ReadVarInt(packet).Length;
		if (item > 0 && item + 1 < packet.Length)
		{
			byte b = packet[item];
			byte b2 = packet[item + 1];
			ushort num = (ushort)((b << 8) | b2);
			_protocolHealth?.Record(num);
			if (_opcodeDispatch == null)
			{
				_opcodeDispatch = BuildOpcodeDispatch();
			}
			if (_opcodeDispatch.TryGetValue(num, out Func<byte[], bool> value) && value(packet))
			{
				LockGameStream();
			}
			else if (TryParseSkillSpecPacket(packet))
			{
				LockGameStream();
			}
			else
			{
				_diagPerfectNoMatch++;
			}
		}
	}

	private DecodedBundle? TryDecompressBundle(byte[] packet, int payloadStart)
	{
		if (payloadStart + 6 > packet.Length)
		{
			return null;
		}
		int num = (packet[payloadStart] & 0xFF) | ((packet[payloadStart + 1] & 0xFF) << 8) | ((packet[payloadStart + 2] & 0xFF) << 16) | ((packet[payloadStart + 3] & 0xFF) << 24);
		if (num <= 0 || num > 1000000)
		{
			_logger.LogTrace("FF FF bundle: invalid decompressed size {Size}, skipping", num);
			return null;
		}
		int num2 = payloadStart + 4;
		byte[] subArray = packet[num2..];
		try
		{
			byte[] array = new byte[num];
			int num3 = LZ4Codec.Decode(subArray, 0, subArray.Length, array, 0, num);
			if (num3 <= 0)
			{
				_logger.LogTrace("FF FF bundle: LZ4 decompression returned {Decoded}", num3);
				return null;
			}
			_logger.LogTrace("\ud83d\udce6 FF FF bundle decompressed: {Compressed}b → {Decompressed}b", subArray.Length, num3);
			byte[] subArray2 = array[..num3];
			ScanForPlayerStats(subArray2);
			ScanForCombatPower(subArray2);
			ScanForCombatPowerBundle(subArray2);
			ScanForLegionRecord(subArray2);
			ScanForSkillLoadout(subArray2);
			ScanForLootEnergyList(subArray2);
			ScanForInventoryList(subArray2);
			List<int> walkedOffsets = ParseDecompressedBundle(array, num3);
			return new DecodedBundle(array, num3, walkedOffsets);
		}
		catch (Exception ex)
		{
			_logger.LogTrace("FF FF bundle: LZ4 decompression failed: {Msg}", ex.Message);
			return null;
		}
	}

	private List<int> ParseDecompressedBundle(byte[] data, int length)
	{
		List<int> list = new List<int>();
		int num = 0;
		while (num < length)
		{
			if (data[num] == 0)
			{
				num++;
				continue;
			}
			var (num2, num3) = ReadVarInt(data, num);
			if (num3 <= 0 || num2 <= 0)
			{
				break;
			}
			int num4 = num2 - 3;
			if (num4 <= 0)
			{
				num++;
				continue;
			}
			if (num + num4 > length)
			{
				break;
			}
			int num5 = Math.Min(num + num2, length);
			byte[] subArray = data[num..num5];
			if (subArray.Length > num3 + 1 && subArray[num3] == byte.MaxValue && subArray[num3 + 1] == byte.MaxValue)
			{
				list.Add(num);
				TryDecompressBundle(subArray, num3 + 2);
			}
			else if (subArray.Length > 3)
			{
				list.Add(num);
				ParsePerfectPacket(subArray);
			}
			num += num4;
		}
		TryScanLocalPlayerRecord((data.Length == length) ? data : data[..length]);
		return list;
	}

	private void ParseBrokenLengthPacket(byte[] packet)
	{
		TryParseGameServerPacket(packet);
		TryParseActionPacket(packet);
		int item = ReadVarInt(packet).Length;
		if (item > 0 && item + 1 < packet.Length && packet[item] == byte.MaxValue && packet[item + 1] == byte.MaxValue)
		{
			int num = item + 2;
			DecodedBundle? decodedBundle = TryDecompressBundle(packet, num);
			int num2 = item + 2 + 4 + 2;
			if (packet.Length > num2 + 1 && packet[num2] == 19 && packet[num2 + 1] == 56)
			{
				TryParseBulkSkillRoster(packet, num2 + 2);
			}
			if (decodedBundle.HasValue)
			{
				DecodedBundle valueOrDefault = decodedBundle.GetValueOrDefault();
				ScanDecodedBundleForSpawns(valueOrDefault);
			}
			else
			{
				ScanForNpcSpawnsInContainer(packet, (num2 > 0) ? num2 : num);
			}
		}
		else
		{
			_logger.LogTrace("Remaining packet buffer: {Length} bytes", packet.Length);
			if (!TryParseHpUpdatePacket(packet) && !TryParseNicknamePacket(packet) && !TryParsePlayerInfoPacket(packet) && !TryParseNpcSpawnPacket(packet, fromDispatch: false))
			{
				TryParseTargetSelectedPacket(packet);
			}
		}
	}

	private bool TryParseDamagePacket(byte[] packet)
	{
		if (packet.Length < 3)
		{
			return false;
		}
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 4 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		_diag04_38Enter++;
		if (num >= packet.Length)
		{
			LogTrunc("targetId", packet, num);
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			LogTrunc("targetId-varint", packet, num);
			return false;
		}
		num += num3;
		_currentTarget = num2;
		if (num >= packet.Length)
		{
			LogTrunc("switchVar", packet, num);
			return false;
		}
		var (num4, num5) = ReadVarInt(packet, num);
		if (num5 < 0)
		{
			LogTrunc("switchVar-varint", packet, num);
			return false;
		}
		num += num5;
		int num6 = num4 & 0xF;
		if (num6 < 4)
		{
			_diagNonDamage++;
			_nonDmgSwitchVarCounts.AddOrUpdate(num6, 1, (int _, int c) => c + 1);
			try
			{
				int num7 = num;
				var (num8, num9) = ReadVarInt(packet, num7);
				if (num9 < 0)
				{
					return true;
				}
				num7 += num9;
				var (num10, num11) = ReadVarInt(packet, num7);
				if (num11 < 0)
				{
					return true;
				}
				num7 += num11;
				if (num7 + 5 > packet.Length)
				{
					return true;
				}
				uint num12 = (uint)(packet[num7] | (packet[num7 + 1] << 8) | (packet[num7 + 2] << 16) | (packet[num7 + 3] << 24));
				byte b = packet[num7 + 4];
				if (num10 != num2 && num12 >= 1218000 && num12 <= 1230000)
				{
					if (num6 == 2 || (num6 == 0 && num8 > 0))
					{
						_logger.LogInformation("\ud83d\udee1\ufe0f EVADE (0x0438 sub<4 NPC skill) — target={Target} actor={Actor} skill={Skill} counter={Counter} sub={Sub} flag={Flag}", num2, num10, num12, b, num6, num8);
						DefenseEventParsed?.Invoke(this, new DefenseEvent
						{
							Timestamp = NowMs(),
							ActorId = num10,
							TargetId = num2,
							SkillCode = (int)num12,
							Outcome = DefenseOutcome.Evade,
							EventCounter = b
						});
						RememberDamageCounter(num10, b);
					}
					else if (num6 == 0 && num8 == 0 && !HasRecentDamageCounter(num10, b))
					{
						_logger.LogInformation("\ud83d\udee1\ufe0f RESIST (0x0438 sub=0 flag=0 NPC skill, no paired damage) — target={Target} actor={Actor} skill={Skill} counter={Counter}", num2, num10, num12, b);
						DefenseEventParsed?.Invoke(this, new DefenseEvent
						{
							Timestamp = NowMs(),
							ActorId = num10,
							TargetId = num2,
							SkillCode = (int)num12,
							Outcome = DefenseOutcome.Resist,
							EventCounter = b
						});
					}
				}
				else if (num10 != num2 && num12 >= 11000000 && num12 < 20000000)
				{
					switch (num6)
					{
					case 0:
						if (num8 > 0)
						{
							break;
						}
						goto end_IL_0127;
					case 2:
						break;
					default:
						goto end_IL_0127;
					}
					_logger.LogInformation("\ud83c\udfaf TARGET AVOIDED (0x0438 sub<4 PC skill) — attacker={Actor} target={Target} skill={Skill} counter={Counter} sub={Sub} flag={Flag}", num10, num2, num12, b, num6, num8);
					DefenseEventParsed?.Invoke(this, new DefenseEvent
					{
						Timestamp = NowMs(),
						ActorId = num10,
						TargetId = num2,
						SkillCode = (int)num12,
						Outcome = DefenseOutcome.TargetAvoided,
						EventCounter = b
					});
				}
				end_IL_0127:;
			}
			catch
			{
			}
			return true;
		}
		if (num >= packet.Length)
		{
			LogTrunc("flag", packet, num);
			return false;
		}
		var (flag, num13) = ReadVarInt(packet, num);
		if (num13 < 0)
		{
			LogTrunc("flag-varint", packet, num);
			return false;
		}
		num += num13;
		if (num >= packet.Length)
		{
			LogTrunc("actorId", packet, num);
			return false;
		}
		var (num14, num15) = ReadVarInt(packet, num);
		if (num15 < 0)
		{
			LogTrunc("actorId-varint", packet, num);
			return false;
		}
		num += num15;
		if (num14 < 100 && !_dataRepository.IsConfirmedPlayer(num14))
		{
			_logger.LogTrace("\ud83d\udeab Null-actor filtered: Actor={Actor} (< {Floor}), Target={Target}, Switch={Switch}", num14, 100, num2, num4);
			return true;
		}
		int num16 = num;
		var (num17, rawSkillCode, num18) = ReadSkillCode(packet, num);
		if (num18 < 0)
		{
			_diagSkillCodeFail++;
			return false;
		}
		int num19 = ((num16 + 4 < packet.Length) ? packet[num16 + 4] : 0);
		num += num18;
		if (num >= packet.Length)
		{
			LogTrunc("type", packet, num);
			return false;
		}
		var (num20, num21) = ReadVarInt(packet, num);
		if (num21 < 0)
		{
			LogTrunc("type-varint", packet, num);
			return false;
		}
		num += num21;
		if (num20 == 36 || num20 == 37 || num20 == 39 || num20 == 40 || num20 == 41 || num20 == 42)
		{
			_diagNoDamageOutcome++;
			return true;
		}
		if (num >= packet.Length)
		{
			LogTrunc("flagsBlock", packet, num);
			return false;
		}
		int num22 = num;
		int num23 = num6 & 3;
		int num24;
		if (num23 == 2)
		{
			int item2 = ReadVarInt(packet, num22 + 1).Length;
			if (item2 < 0)
			{
				LogTrunc("flagsBlock-scale-varint", packet, num22 + 1);
				return false;
			}
			num24 = 10 + item2;
			if (item2 > 1)
			{
				_diagWideFlagsBlock++;
			}
		}
		else if (num23 == 0)
		{
			num24 = 8;
		}
		else
		{
			num24 = ((num23 == 1) ? 13 : 15);
			_diagUnprovenFlagsBlock++;
		}
		if (num22 + num24 > packet.Length)
		{
			LogTrunc($"flagsBlock-size({num24})", packet, num);
			return false;
		}
		SpecialDamageFlags specialFlags = ParseSpecialDamageFlags(packet[num22..(num22 + num24)]);
		int num25 = ((num24 >= 10) ? packet[num22] : 0);
		num += num24;
		if (num >= packet.Length)
		{
			LogTrunc("unknown", packet, num);
			return false;
		}
		var (num26, num27) = ReadVarInt(packet, num);
		if (num27 < 0)
		{
			LogTrunc("unknown-varint", packet, num);
			return false;
		}
		num += num27;
		if (num >= packet.Length)
		{
			LogTrunc("damage", packet, num);
			return false;
		}
		var (num28, num29) = ReadVarInt(packet, num);
		if (num29 < 0)
		{
			LogTrunc("damage-varint", packet, num);
			return false;
		}
		num += num29;
		int num30 = 0;
		if (num < packet.Length)
		{
			var (num31, num32) = ReadVarInt(packet, num);
			if (num32 > 0)
			{
				num30 = num31;
				num += num32;
			}
		}
		int num33 = 0;
		int num34 = 0;
		if (num < packet.Length)
		{
			var (num35, num36) = ReadVarInt(packet, num);
			if (num36 > 0 && num35 >= 1 && num35 <= 10)
			{
				num += num36;
				for (int num37 = 0; num37 < num35; num37++)
				{
					if (num >= packet.Length)
					{
						break;
					}
					var (num38, num39) = ReadVarInt(packet, num);
					if (num39 <= 0)
					{
						break;
					}
					num += num39;
					if (num38 > 0 && num38 < 10000000)
					{
						num33++;
						num34 += num38;
					}
				}
			}
		}
		if (num24 >= 4 && num30 == 0)
		{
			uint num40 = (uint)(packet[num22 + num24 - 4] | (packet[num22 + num24 - 3] << 8) | (packet[num22 + num24 - 2] << 16) | (packet[num22 + num24 - 1] << 24));
			if (num40 > 16)
			{
				_logger.LogTrace("\ud83d\udc7b PhantomFiltered (loop=0 + non-canonical trail): Actor={Actor}, Skill={Skill}, Damage={Damage}, fbTrail={Trail}", num14, num17, num28, num40);
				return true;
			}
		}
		if (num20 < 0 || num20 > 72 || num28 <= 0)
		{
			_diagStructFail++;
			_logger.LogTrace("⚠\ufe0f StructFail: Target={Target}, Actor={Actor}, Type={Type}, Damage={Damage}, Skill={Skill}, Switch={Switch}, Unknown={Unknown}", num2, num14, num20, num28, num17, num4, num26);
			return false;
		}
		if (num28 > 10000000)
		{
			_logger.LogDebug("\ud83d\udeab InsaneMagnitude drop (04 38): Skill={Skill}, Actor={Actor}, Target={Target}, Damage={Damage:N0} > ceiling {Ceiling:N0} — phantom, not a real hit", num17, num14, num2, num28, 10000000);
			return true;
		}
		int skillCode = num17 / 10000 * 10000;
		bool flag2 = false;
		if (_skillRepository != null)
		{
			if (_skillRepository.IsHealSkill(skillCode) || _skillRepository.IsProvenHeal(num17))
			{
				flag2 = true;
				_logger.LogTrace("\ud83d\udc9a Heal: Skill={Skill} ({SkillName}), Actor={Actor}, Target={Target}, Amount={Amount:N0}, Type={Type}", num17, _skillRepository.GetSkillName(skillCode), num14, num2, num28, num20);
			}
			if (!_skillRepository.HasSkill(num17) && (num17 < 11000000 || num17 > 20000000) && (num17 < 1000000 || num17 > 5000000) && (num17 < 100000 || num17 > 200000))
			{
				_logger.LogTrace("\ud83d\udeab BLOCKED garbage skill: Skill={Skill}, Damage={Damage:N0}", num17, num28);
				_diagWhitelistReject++;
				return true;
			}
		}
		if (num14 == num2 && !flag2)
		{
			_diagSelfDamage++;
			_logger.LogTrace("\ud83d\udeab Self-damage filtered: Actor=Target={Actor}, Skill={Skill}, Damage={Damage}, Type={Type}", num14, num17, num28, num20);
			return true;
		}
		(HitDirection Direction, HitModifier Modifier) tuple14 = HitQualityDecoder.DecodeWireQuality(packet.AsSpan(num22, num24));
		HitDirection item3 = tuple14.Direction;
		HitModifier item4 = tuple14.Modifier;
		int chainIndex = HitQualityDecoder.DecodeChainIndex(packet.AsSpan(num22, num24));
		bool isBlocked = false;
		if (num25 != 0)
		{
			int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
			bool actorIsKnownPlayer = num14 == localPlayerEntityId || _dataRepository.IsConfirmedPlayer(num14);
			bool targetIsKnownPlayer = num2 == localPlayerEntityId || _dataRepository.IsConfirmedPlayer(num2);
			int directionByte = ((num23 == 2) ? packet[num22 + num24 - 9] : (-1));
			isBlocked = BlockFlagPolicy.IsBlocked(actorIsKnownPlayer, targetIsKnownPlayer, num17, num4, num25, directionByte);
		}
		DamageEvent damageEvent = new DamageEvent
		{
			Timestamp = NowMs(),
			ActorId = num14,
			TargetId = num2,
			Damage = num28,
			SkillCode = num17,
			RawSkillCode = rawSkillCode,
			Type = num20,
			Flag = flag,
			SwitchVariable = num4,
			Loop = num30,
			Unknown = num26,
			EventCounter = num19,
			SpecialFlags = specialFlags,
			SpecialFlagsMeasured = (num24 >= 10),
			IsBlocked = isBlocked,
			MultiHitCount = num33,
			MultiHitDamage = num34,
			Direction = item3,
			Modifier = item4,
			ChainIndex = chainIndex,
			MultiHitProc = HitQualityDecoder.DecodeMultiHit(num4),
			IsDoT = false,
			IsPassive = false,
			IsHeal = flag2
		};
		if (IsDuplicateDamageEvent(num14, num2, rawSkillCode, num19, num28))
		{
			return true;
		}
		_diagEmitted++;
		_diagTotalDamage += num28 + num34;
		long ticks = DateTime.UtcNow.Ticks;
		if (ticks - _lastDiagLog > 100000000)
		{
			long num41 = _packetCaptureService?.TcpRetransmissions ?? 0;
			long num42 = _packetCaptureService?.TcpGaps ?? 0;
			long num43 = _packetCaptureService?.TcpGapBytes ?? 0;
			string text = string.Join(", ", from kv in _nonDmgSwitchVarCounts
				orderby kv.Key
				select $"sv{kv.Key}={kv.Value}");
			int num44 = _diagNonDamage + _diagSkillCodeFail + _diagStructFail + _diagTruncated + _diagSelfDamage + _diagWhitelistReject + _diagEmitted + _diagNoDamageOutcome + _diagDedupHits;
			int num45 = _diag04_38Enter - num44;
			long num46 = 0L;
			long num47 = 0L;
			if (_packetCaptureService != null)
			{
				(long Received, long Dropped) captureStatistics = _packetCaptureService.GetCaptureStatistics();
				long item5 = captureStatistics.Received;
				long item6 = captureStatistics.Dropped;
				num46 = item5;
				num47 = item6;
			}
			_logger.LogDebug("\ud83d\udcca DAMAGE DIAG: Enter={Enter}, NonDmg={NonDmg} [{SwitchDist}], SkillFail={SkillFail}, StructFail={StructFail}, Trunc={Trunc}, Self={Self}, WL_Reject={WLReject}, NoDmgOutcome={NoDmgOut}, Dedup={Dedup}, Emitted={Emitted}, TotalDmg={TotalDmg:N0}, ★UNACCOUNTED={Unaccounted}★ | Routing: Perfect={Perfect}, Split={Split}, Broken={Broken}, NoMatch={NoMatch} | CarryOver: Saved={COSaved}, Recovered={CORecovered} | EmbeddedScanner={EmbScanner} | DoT: Enter={DotEnter}, Emitted={DotEmit}, Self={DotSelf}, ParseFail={DotFail}, ZeroDmg={DotZero}, Skip08Reject={DotSkip08}, TotalDmg={DotDmg:N0} | Npcap: KernelRx={NpcapRx}, ★KernelDrop={NpcapDrop}★ | TcpRetrans={TcpRetrans}, TcpGaps={TcpGaps}, GapBytes={GapBytes} | FlagsBlock: Wide={WideFB}, Unproven={UnprovenFB}", _diag04_38Enter, _diagNonDamage, text, _diagSkillCodeFail, _diagStructFail, _diagTruncated, _diagSelfDamage, _diagWhitelistReject, _diagNoDamageOutcome, _diagDedupHits, _diagEmitted, _diagTotalDamage, num45, _diagPerfectTotal, _diagSplitCount, _diagBrokenCount, _diagPerfectNoMatch, Interlocked.Read(in _diagCarryOverSaved), Interlocked.Read(in _diagCarryOverRecovered), _diagEmbeddedScannerEmit, _diagDotEnter, _diagDotEmitted, _diagDotSelfDmg, _diagDotParseFail, _diagDotZeroDmg, _diagDotSkip08Reject, _diagDotTotalDamage, num46, num47, num41, num42, num43, _diagWideFlagsBlock, _diagUnprovenFlagsBlock);
			_lastDiagLog = ticks;
		}
		DamageEventParsed?.Invoke(this, damageEvent);
		if (num14 != num2 && num28 > 0)
		{
			RememberDamageCounter(num14, num19);
		}
		if (damageEvent.IsBlocked && num14 != num2 && num28 >= 0)
		{
			DefenseEventParsed?.Invoke(this, new DefenseEvent
			{
				Timestamp = NowMs(),
				ActorId = num14,
				TargetId = num2,
				SkillCode = num17,
				Outcome = DefenseOutcome.Block,
				EventCounter = num19,
				Damage = num28,
				FlagByte = num25
			});
		}
		if (!flag2)
		{
			long totalHitDamage = (long)num28 + (long)num34;
			_entityCumulativeDamage.AddOrUpdate(num2, totalHitDamage, (int _, long prev) => prev + totalHitDamage);
		}
		return true;
	}

	private bool TryParseTargetSelectedPacket(byte[] packet)
	{
		return false;
	}

	private bool TryParseActionPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 2 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		_ = ReadVarInt(packet, num).Length;
		_ = 0;
		return true;
	}

	private bool TryParseBatchContainer(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 61 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		int num2 = 0;
		int num3 = 20;
		while (num < packet.Length - 2 && num2 < num3)
		{
			var (num4, num5) = ReadVarInt(packet, num);
			if (num5 <= 0 || num4 <= 3)
			{
				break;
			}
			int num6 = num;
			int num7 = num + num4;
			if (num7 > packet.Length)
			{
				num7 = packet.Length;
			}
			if (num7 <= num6 + num5)
			{
				break;
			}
			byte[] subArray = packet[num6..num7];
			ParsePerfectPacket(subArray);
			num2++;
			num = num6 + num4 - 3;
		}
		if (num2 > 0)
		{
			_logger.LogTrace("\ud83d\udce6 3D 38 BATCH: {Count} sub-packets extracted from {Len}-byte container", num2, packet.Length);
		}
		return num2 > 0;
	}

	private bool TryParseDoTPacket(byte[] packet)
	{
		int num = 0;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 5 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		_diagDotEnter++;
		if (num >= packet.Length)
		{
			_diagDotParseFail++;
			return false;
		}
		var (num4, num5) = ReadVarInt(packet, num);
		if (num5 < 0)
		{
			_diagDotParseFail++;
			return false;
		}
		num += num5;
		if (num >= packet.Length)
		{
			_diagDotParseFail++;
			return false;
		}
		byte b = packet[num];
		num++;
		if (num >= packet.Length)
		{
			_diagDotParseFail++;
			return false;
		}
		var (num6, num7) = ReadVarInt(packet, num);
		if (num7 < 0)
		{
			_diagDotParseFail++;
			return false;
		}
		if (num6 == num4)
		{
			_diagDotSelfDmg++;
		}
		num += num7;
		if (num6 < 100 && !_dataRepository.IsConfirmedPlayer(num6))
		{
			_logger.LogTrace("\ud83d\udeab Null-actor filtered (DoT): Actor={Actor} (< {Floor}), Target={Target}", num6, 100, num4);
			return true;
		}
		if (num >= packet.Length)
		{
			_diagDotParseFail++;
			return false;
		}
		var (num8, num9) = ReadVarInt(packet, num);
		if (num9 < 0)
		{
			_diagDotParseFail++;
			return false;
		}
		num += num9;
		if (num + 4 > packet.Length)
		{
			_diagDotParseFail++;
			return false;
		}
		int num10 = ParseUInt32LE(packet, num) / 100;
		num += 4;
		if (num10 <= 0)
		{
			_diagDotWhitelistReject++;
			return true;
		}
		if (_skillRepository != null && !_skillRepository.HasSkill(num10))
		{
			_diagDotWhitelistReject++;
			_logger.LogTrace("\ud83d\udeab BLOCKED garbage DoT skill: Skill={Skill}, Target={Target}, Actor={Actor}", num10, num4, num6);
			return true;
		}
		if (_skillRepository != null && _skillRepository.IsNonDamageSkill(num10))
		{
			if (HealingPolicy.IsProvenHealOverTimeSkill(num10))
			{
				TryEmitHealOverTimeTick(packet, num, b, num4, num6, num8, num10);
			}
			_diagDotWhitelistReject++;
			_logger.LogTrace("\ud83d\udeab BLOCKED non-damage (Buff/Heal/Spawn) DoT skill: Skill={Skill}, Target={Target}, Actor={Actor}", num10, num4, num6);
			return true;
		}
		if (b == 8 && num + 4 == num3 + num2 - 4)
		{
			_diagDotSkip08Reject++;
			return true;
		}
		if (num >= packet.Length)
		{
			_diagDotParseFail++;
			return false;
		}
		var (num11, num12) = ReadVarInt(packet, num);
		if (num12 < 0)
		{
			_diagDotParseFail++;
			return false;
		}
		num += num12;
		if (num11 <= 0)
		{
			_diagDotZeroDmg++;
			return false;
		}
		int num13 = num6;
		int num14 = num4;
		int effectiveDamage = num11;
		(int, int, int, int) tuple6 = (num4, num6, num8, SkillCodeNormalizer.Normalize(num10));
		int num15 = num;
		(int Value, int Length) tuple7 = ReadVarInt(packet, num15);
		int item = tuple7.Value;
		int item2 = tuple7.Length;
		bool flag = false;
		if ((b & 1) != 0 && item2 >= 0 && num15 + item2 + 4 <= packet.Length)
		{
			flag = SkillCodeNormalizer.Normalize(ParseUInt32LE(packet, num15 + item2)) == SkillCodeNormalizer.Normalize(num10);
		}
		if (flag)
		{
			_diagDotPoolForm++;
			RememberPoolStream(tuple6);
			if (num4 == num6)
			{
				return true;
			}
			num13 = num4;
			num14 = num6;
			effectiveDamage = item;
			num = num15 + item2;
			if (effectiveDamage <= 0)
			{
				_diagDotZeroDmg++;
				return true;
			}
		}
		else if ((b == 10 || b == 11) && _poolStreamKeys.Contains(tuple6))
		{
			_diagDotPoolResidualDrop++;
			return true;
		}
		if (num13 < 100 && !_dataRepository.IsConfirmedPlayer(num13))
		{
			return true;
		}
		if (effectiveDamage > 10000000)
		{
			_logger.LogDebug("\ud83d\udeab InsaneMagnitude drop (05 38 DoT): Skill={Skill}, Actor={Actor}, Target={Target}, Damage={Damage:N0} > ceiling {Ceiling:N0} — phantom, not a real tick", num10, num13, num14, effectiveDamage, 10000000);
			return true;
		}
		if (IsDuplicateDotTick(packet, num3, num))
		{
			return true;
		}
		_diagDotEmitted++;
		_diagDotTotalDamage += effectiveDamage;
		DamageEvent e = new DamageEvent
		{
			Timestamp = NowMs(),
			ActorId = num13,
			TargetId = num14,
			Damage = effectiveDamage,
			SkillCode = num10,
			IsDoT = true
		};
		_logger.LogTrace("DoT: Actor={Actor}, Target={Target}, Skill={Skill}, Damage={Damage}", num13, num14, num10, effectiveDamage);
		DamageEventParsed?.Invoke(this, e);
		_entityCumulativeDamage.AddOrUpdate(num14, effectiveDamage, (int _, long prev) => prev + effectiveDamage);
		return true;
	}

	private bool TryParseHpUpdatePacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 0 || packet[num + 1] != 141)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num + 2 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 2 || packet[num + 1] != 1 || packet[num + 2] != 0)
		{
			return false;
		}
		num += 3;
		if (num + 3 >= packet.Length)
		{
			return false;
		}
		uint num4 = (uint)((packet[num] & 0xFF) | ((packet[num + 1] & 0xFF) << 8) | ((packet[num + 2] & 0xFF) << 16) | ((packet[num + 3] & 0xFF) << 24));
		_framedSpawnMaxHp.TryGetValue(num2, out var value);
		if (num4 > 500000000 && !BossHpResetPolicy.IsAboveCapWithinFramedSpawnMax(num4, 500000000u, value))
		{
			_logger.LogTrace("⚠\ufe0f HP filtered (exceeds cap): Entity={EntityId}, HP={Hp:N0}", num2, num4);
			return true;
		}
		_spawnMaxHp.TryGetValue(num2, out var value2);
		uint? maxHp = ((value2 != 0) ? new uint?(value2) : ((uint?)null));
		HpUpdateEventParsed?.Invoke(this, new HpUpdateEvent
		{
			Timestamp = NowMs(),
			ActorId = num2,
			CurrentHp = num4,
			MaxHp = maxHp,
			Source = HpReadingSource.HpUpdate008D,
			FramedSpawnMaxHp = ((value != 0) ? new uint?(value) : ((uint?)null))
		});
		if (maxHp.HasValue && maxHp.Value >= 200000 && _entityCumulativeDamage.TryGetValue(num2, out var value3))
		{
			_entityFirstHp.TryAdd(num2, num4);
			_entityLastHp[num2] = num4;
			uint num5 = _entityFirstHp[num2];
			if (num5 > num4)
			{
				long num6 = (long)num5 - (long)num4;
				long num7 = num6 - value3;
				double num8 = ((num6 > 0) ? ((double)num7 / (double)num6 * 100.0) : 0.0);
				long tickCount = Environment.TickCount64;
				if (num4 == 0 || tickCount - _lastGapLogTicks > 5000)
				{
					_lastGapLogTicks = tickCount;
					string text = ((num4 == 0) ? "\ud83c\udfc6 FINAL ACCURACY" : "\ud83d\udcd0 ACCURACY GAP");
					_logger.LogDebug("{Label}: Entity={Entity}, HPdrop={HpDrop:N0}, CumDmg={CumDmg:N0}, Gap={Gap:+#,0;-#,0;0} ({GapPct:+0.0;-0.0;0.0}%), HP={Hp:N0}/{MaxHp:N0}", text, num2, num6, value3, num7, num8, num4, maxHp.Value);
				}
			}
		}
		return true;
	}

	private bool TryParseSummonPacket(byte[] packet, bool fromDispatch)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 2 >= packet.Length)
		{
			return false;
		}
		if ((packet[num] != 64 && packet[num] != 65 && packet[num] != 66) || packet[num + 1] != 54)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num >= packet.Length)
		{
			return false;
		}
		byte b = packet[num];
		num++;
		int afterFlag = num;
		if ((b & 0x1F) != 31)
		{
			return false;
		}
		_protocolHealth?.RecordFeature("summon_spawn_observed");
		if (num >= packet.Length)
		{
			return false;
		}
		num++;
		if (num >= packet.Length)
		{
			return false;
		}
		byte b2 = packet[num];
		num++;
		if (b2 == 1)
		{
			var (num4, num5) = ReadVarInt(packet, num);
			if (num5 < 0)
			{
				return false;
			}
			num += num5 + num4;
		}
		int num6 = FindArrayIndex(packet, new byte[8] { 255, 255, 255, 255, 255, 255, 255, 255 });
		if (num6 == -1)
		{
			return false;
		}
		byte[] subArray = packet[(num6 + 8)..];
		int num7 = FindArrayIndex(subArray, new byte[3] { 7, 2, 6 });
		if (num7 == -1)
		{
			num7 = FindArrayIndex(subArray, new byte[3] { 7, 2, 1 });
		}
		if (num7 == -1)
		{
			return false;
		}
		int num8 = num6 + 8 + num7 + 3;
		if (num8 + 2 > packet.Length)
		{
			return false;
		}
		int num9 = ParseUInt16LE(packet, num8);
		_logger.LogTrace("Summon mapping: Summoner={SummonerId}, Summon={SummonId}", num9, num2);
		if (fromDispatch)
		{
			SummonAppeared?.Invoke(this, (num2, num9, ReadSummonNpcId(packet, afterFlag), NowMs()));
		}
		SummonDetected?.Invoke(this, (num9, num2));
		_protocolHealth?.RecordFeature("summon_owner");
		return true;
	}

	private int ReadSummonNpcId(byte[] packet, int afterFlag1)
	{
		int num = afterFlag1 + 3;
		if (num >= packet.Length)
		{
			return 0;
		}
		switch (packet[num++])
		{
		case 1:
			var (num2, num3) = ReadVarInt(packet, num);
			if (num3 < 0 || num2 < 0)
			{
				return 0;
			}
			num += num3 + num2;
			break;
		default:
			return 0;
		case 0:
			break;
		}
		if (num < 0 || num + 6 > packet.Length)
		{
			return 0;
		}
		int num4 = packet[num] | (packet[num + 1] << 8) | (packet[num + 2] << 16) | (packet[num + 3] << 24);
		if (num4 < 1000000 || num4 > 10000000 || packet[num + 5] != 2)
		{
			return 0;
		}
		return num4;
	}

	private bool TryParseSummonSpawnPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 53 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0 || num2 <= 0)
		{
			return false;
		}
		num += num3;
		if (num >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 0)
		{
			return false;
		}
		num++;
		(int Value, int Length) tuple2 = ReadVarInt(packet, num);
		var (num4, _) = tuple2;
		if (tuple2.Length < 0 || num4 <= 0)
		{
			return false;
		}
		if (num2 == num4)
		{
			return false;
		}
		if (_discoveredEntities.ContainsKey(num2))
		{
			return true;
		}
		if (_dataRepository.IsPresentConfirmedPlayer(num2))
		{
			return true;
		}
		_logger.LogInformation("\ud83d\udd17 SUMMON SPAWN (35 38): Entity={EntityId} → Owner={OwnerId}", num2, num4);
		SummonDetected?.Invoke(this, (num4, num2));
		_protocolHealth?.RecordFeature("summon_owner");
		return true;
	}

	private void ScanForEmbeddedNicknames(byte[] packet)
	{
		for (int i = 2; i < packet.Length - 5; i++)
		{
			if (packet[i] != 1 || packet[i + 1] != 7)
			{
				continue;
			}
			int num = packet[i + 2] & 0xFF;
			if (num < 2 || num > 40 || i + 3 + num > packet.Length)
			{
				continue;
			}
			byte[] subArray = packet[(i + 3)..(i + 3 + num)];
			string text = SanitizeNickname(Encoding.UTF8.GetString(subArray));
			if (text == null)
			{
				continue;
			}
			for (int j = 2; j <= 6; j++)
			{
				int num2 = i - j;
				if (num2 < 0)
				{
					continue;
				}
				var (num3, num4) = ReadVarInt(packet, num2);
				if (num4 > 0 && num4 <= 3 && num3 >= 500 && num3 <= 999999)
				{
					int num5 = i - (num2 + num4);
					if (num5 >= 0 && num5 <= 4)
					{
						_logger.LogInformation("✓ NICKNAME EXTRACTED (40 36 embedded): PlayerID={PlayerId}, Nickname='{Nickname}'", num3, text);
						NicknameDiscovered?.Invoke(this, (num3, text, 0, false));
						return;
					}
				}
			}
		}
	}

	private void RememberDamageCounter(int actor, int counter)
	{
		lock (_recentDmgCountersLock)
		{
			(int, int) tuple = (actor, counter);
			if (_recentMissOrHitCounters.Add(tuple))
			{
				_recentMissOrHitCountersOrder.AddLast(tuple);
			}
			while (_recentMissOrHitCounters.Count > 1024)
			{
				(int, int) value = _recentMissOrHitCountersOrder.First.Value;
				_recentMissOrHitCountersOrder.RemoveFirst();
				_recentMissOrHitCounters.Remove(value);
			}
		}
	}

	private bool HasRecentDamageCounter(int actor, int counter)
	{
		lock (_recentDmgCountersLock)
		{
			return _recentMissOrHitCounters.Contains((actor, counter));
		}
	}

	private long NowMs()
	{
		return (ClockOverrideMs?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) - t_segmentAgeMs;
	}

	private long DedupClockMs()
	{
		return (ClockOverrideMs?.Invoke() ?? Environment.TickCount64) - t_segmentAgeMs;
	}

	private bool IsDuplicateDamageEvent(int actorId, int targetId, int rawSkillCode, int counter, int damage)
	{
		long num = DedupClockMs();
		(int, int, int, int, int) tuple = (actorId, targetId, rawSkillCode, counter, damage);
		while (_damageEventDedupOrder.Count > 0 && (num - _damageEventDedupOrder.Peek().Ts > 3000 || _damageEventDedupOrder.Count > 16384))
		{
			var (key, num2) = _damageEventDedupOrder.Dequeue();
			if (_damageEventDedup.TryGetValue(key, out var value) && value == num2)
			{
				_damageEventDedup.Remove(key);
			}
		}
		bool num3 = _damageEventDedup.TryGetValue(tuple, out var value2) && num - value2 <= 3000;
		_damageEventDedup[tuple] = num;
		_damageEventDedupOrder.Enqueue((tuple, num));
		if (num3)
		{
			_diagDedupHits++;
		}
		return num3;
	}

	private void RememberPoolStream((int, int, int, int) key)
	{
		if (_poolStreamKeys.Add(key))
		{
			_poolStreamOrder.Enqueue(key);
			while (_poolStreamKeys.Count > 8192)
			{
				_poolStreamKeys.Remove(_poolStreamOrder.Dequeue());
			}
		}
	}

	private void TryEmitHealOverTimeTick(byte[] packet, int offset, byte skipByte, int recipientId, int healerId, int instance, int skillCode)
	{
		if ((skipByte & 1) == 0 || (recipientId < 100 && !_dataRepository.IsConfirmedPlayer(recipientId)) || offset < 4)
		{
			return;
		}
		int item = ParseUInt32LE(packet, offset - 4);
		var (num, num2) = ReadVarInt(packet, offset);
		if (num2 < 0 || num < 0)
		{
			return;
		}
		offset += num2;
		var (num3, num4) = ReadVarInt(packet, offset);
		if (num4 < 0 || num3 <= 0 || num3 > 10000000)
		{
			return;
		}
		offset += num4;
		if (offset + 4 <= packet.Length && SkillCodeNormalizer.Normalize(ParseUInt32LE(packet, offset)) == SkillCodeNormalizer.Normalize(skillCode))
		{
			long num5 = NowMs();
			if (!IsDuplicateHealTick((recipientId, healerId, instance, item, num, num3), num5))
			{
				HealTickParsed?.Invoke(this, new DamageEvent
				{
					Timestamp = num5,
					ActorId = healerId,
					TargetId = recipientId,
					Damage = num3,
					SkillCode = skillCode,
					IsDoT = true,
					IsHeal = true
				});
			}
		}
	}

	private bool IsDuplicateHealTick((int, int, int, int, int, int) key, long nowMs)
	{
		if (_healTickSeenMs.TryGetValue(key, out var value) && nowMs - value < 5000)
		{
			return true;
		}
		_healTickSeenMs[key] = nowMs;
		if (_healTickSeenMs.Count > 4096)
		{
			foreach (var item in (from kv in _healTickSeenMs
				where nowMs - kv.Value >= 5000
				select kv.Key).ToList())
			{
				_healTickSeenMs.Remove(item);
			}
		}
		return false;
	}

	private bool IsDuplicateDotTick(byte[] packet, int opcodeOffset, int endOffset)
	{
		int num = Math.Min(endOffset, packet.Length) - opcodeOffset;
		if (num < 8)
		{
			return false;
		}
		long num2 = DedupClockMs() / 500;
		string text = BitConverter.ToString(packet, opcodeOffset, num) + "#" + num2;
		if (_embeddedDedupSet.Contains(text))
		{
			_diagDedupHits++;
			return true;
		}
		_embeddedDedupSet.Add(text);
		_embeddedDedupOrder.AddLast(text);
		while (_embeddedDedupSet.Count > 16384)
		{
			string value = _embeddedDedupOrder.First.Value;
			_embeddedDedupOrder.RemoveFirst();
			_embeddedDedupSet.Remove(value);
		}
		return false;
	}

	private void ScanForEmbeddedDamage(byte[] payload, int baseOffset = 0, List<(int Start, int End)>? compressedExtents = null)
	{
		if (payload.Length < 15)
		{
			return;
		}
		for (int i = 1; i < payload.Length - 12; i++)
		{
			if ((payload[i] != 4 && payload[i] != 5) || payload[i + 1] != 56 || (payload[i] == 4 && InExtent(compressedExtents, baseOffset + i)))
			{
				continue;
			}
			for (int j = 1; j <= 3 && i - j >= 0; j++)
			{
				(int Value, int Length) tuple = ReadVarInt(payload, i - j);
				var (num, _) = tuple;
				if (tuple.Length != j || num < 10 || num > 80)
				{
					continue;
				}
				int num2 = i - j;
				int num3 = Math.Min(num2 + num, payload.Length);
				if (num3 - num2 >= 10)
				{
					byte[] subArray = payload[num2..num3];
					int num4 = _diagEmitted + _diagDotEmitted;
					if (payload[i] == 4)
					{
						TryParseDamagePacket(subArray);
					}
					else
					{
						TryParseDoTPacket(subArray);
					}
					if (_diagEmitted + _diagDotEmitted > num4)
					{
						_diagEmbeddedScannerEmit++;
					}
					break;
				}
			}
		}
	}

	private void ScanDecodedBundleForSpawns(DecodedBundle bundle)
	{
		byte[] data = bundle.Data;
		int length = bundle.Length;
		for (int i = 1; i < length - 1; i++)
		{
			if ((data[i] != 64 && data[i] != 65 && data[i] != 66) || data[i + 1] != 54)
			{
				continue;
			}
			for (int j = 1; j <= 3 && i - j >= 0; j++)
			{
				int num = i - j;
				(int Value, int Length) tuple = ReadVarInt(data, num);
				var (num2, _) = tuple;
				if (tuple.Length != j || num2 < 15 || num2 > 500)
				{
					continue;
				}
				if (num + num2 - 3 <= length && bundle.WalkedOffsets.BinarySearch(num) < 0)
				{
					byte[] subArray = data[num..Math.Min(num + num2, length)];
					if (!TryParseSummonPacket(subArray, fromDispatch: false))
					{
						TryParseNpcSpawnPacket(subArray, fromDispatch: false);
					}
				}
				break;
			}
		}
	}

	private void ScanForNpcSpawnsInContainer(byte[] packet, int startOffset)
	{
		for (int i = startOffset; i < packet.Length - 1; i++)
		{
			if ((packet[i] != 64 && packet[i] != 65 && packet[i] != 66) || packet[i + 1] != 54)
			{
				continue;
			}
			bool flag = false;
			for (int j = 1; j <= 3 && i - j >= startOffset; j++)
			{
				int num = i - j;
				var (num2, num3) = ReadVarInt(packet, num);
				if (num3 != j || num2 < 15 || num2 > 500 || num + num3 >= packet.Length - 1 || (packet[num + num3] != 64 && packet[num + num3] != 65 && packet[num + num3] != 66) || packet[num + num3 + 1] != 54)
				{
					continue;
				}
				int num4 = num3 + num2;
				if (num + num4 > packet.Length)
				{
					int num5 = num2 - 3;
					if (num5 > 0 && num + num5 <= packet.Length)
					{
						byte[] subArray = packet[num..(num + num5)];
						if (TryParseSummonPacket(subArray, fromDispatch: false) || TryParseNpcSpawnPacket(subArray, fromDispatch: false, identityOnly: true))
						{
							i = num + num5 - 1;
							flag = true;
						}
					}
				}
				else
				{
					byte[] subArray2 = packet[num..(num + num4)];
					if (TryParseSummonPacket(subArray2, fromDispatch: false) || TryParseNpcSpawnPacket(subArray2, fromDispatch: false, identityOnly: true))
					{
						i = num + num4 - 1;
						flag = true;
					}
				}
				break;
			}
			if (flag || i + 12 > packet.Length)
			{
				continue;
			}
			int num6 = i + 2;
			var (num7, num8) = ReadVarInt(packet, num6);
			if (num8 < 0 || num7 <= 0)
			{
				continue;
			}
			num6 += num8;
			if (num6 + 7 > packet.Length)
			{
				continue;
			}
			num6++;
			ushort num9 = (ushort)((packet[num6] & 0xFF) | ((packet[num6 + 1] & 0xFF) << 8));
			num6 += 2;
			int num10 = 0;
			int num11 = -1;
			int[] array = new int[2]
			{
				num6 + 2,
				num6
			};
			foreach (int num12 in array)
			{
				if (num12 + 5 < packet.Length)
				{
					int num13 = (packet[num12] & 0xFF) | ((packet[num12 + 1] & 0xFF) << 8) | ((packet[num12 + 2] & 0xFF) << 16) | ((packet[num12 + 3] & 0xFF) << 24);
					if (num13 >= 1000000 && num13 <= 10000000 && packet[num12 + 5] == 2)
					{
						num10 = num13;
						num11 = num12;
						break;
					}
				}
			}
			if (num11 >= 0)
			{
				_logger.LogInformation("\ud83d\udc3e NPC SPAWN (40 36, unframed, identity only): EntityId={EntityId}, NpcId={NpcId}, Flags=0x{Flags:X4}", num7, num10, num9);
				_assignedBroadcastNpcIds.Add(num10);
				_discoveredEntities[num7] = true;
				NpcTemplateDiscovered?.Invoke(this, (num7, num10));
			}
		}
	}

	private bool TryParseNpcSpawnPacket(byte[] packet, bool fromDispatch, bool identityOnly = false)
	{
		if (!TryReadNpcSpawnHeader(packet, out var entityId, out var _, out var flags, out var npcId, out var markerOffset))
		{
			return false;
		}
		if (markerOffset + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[markerOffset + 1] != 2)
		{
			return false;
		}
		byte b = packet[markerOffset];
		markerOffset += 2;
		if (identityOnly)
		{
			_logger.LogInformation("\ud83d\udc3e NPC SPAWN (40 36, identity only): EntityId={EntityId}, NpcId={NpcId}, Flags=0x{Flags:X4}, Marker=0x{Marker:X2} [{Stream}]", entityId, npcId, flags, b, _currentPayloadStreamLabel);
		}
		else
		{
			_logger.LogInformation("\ud83d\udc3e NPC SPAWN (40 36): EntityId={EntityId}, NpcId={NpcId}, Flags=0x{Flags:X4}, Marker=0x{Marker:X2} [{Stream}]", entityId, npcId, flags, b, _currentPayloadStreamLabel);
		}
		if (!identityOnly)
		{
			NpcAppeared?.Invoke(this, (entityId, npcId, NowMs(), fromDispatch));
			_framedSpawnNpcId[entityId] = npcId;
		}
		_assignedBroadcastNpcIds.Add(npcId);
		_discoveredEntities[entityId] = true;
		NpcTemplateDiscovered?.Invoke(this, (entityId, npcId));
		if (identityOnly)
		{
			return true;
		}
		if ((b & 8) != 0)
		{
			return true;
		}
		if (markerOffset + 19 >= packet.Length)
		{
			return true;
		}
		markerOffset += 19;
		var (num, num2) = ReadVarInt(packet, markerOffset);
		if (num2 < 0 || num <= 0)
		{
			return true;
		}
		markerOffset += num2;
		(int Value, int Length) tuple2 = ReadVarInt(packet, markerOffset);
		var (num3, _) = tuple2;
		if (tuple2.Length < 0 || num3 <= 0)
		{
			return true;
		}
		_spawnMaxHp[entityId] = (uint)num3;
		_framedSpawnMaxHp[entityId] = (uint)num3;
		_logger.LogDebug("\ud83d\udc3e NPC SPAWN HP: EntityId={EntityId}, MaxHp={MaxHp:N0}", entityId, num3);
		HpUpdateEventParsed?.Invoke(this, new HpUpdateEvent
		{
			Timestamp = NowMs(),
			ActorId = entityId,
			CurrentHp = (uint)num3,
			MaxHp = (uint)num3,
			Source = HpReadingSource.FramedSpawn,
			FramedSpawnMaxHp = (uint)num3,
			SpawnNpcId = npcId
		});
		return true;
	}

	private bool TryReadNpcSpawnHeader(byte[] packet, out int entityId, out byte flag1, out ushort flags, out int npcId, out int markerOffset)
	{
		entityId = 0;
		flag1 = 0;
		flags = 0;
		npcId = 0;
		markerOffset = -1;
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if ((packet[num] != 64 && packet[num] != 65 && packet[num] != 66) || packet[num + 1] != 54)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0 || num2 <= 0)
		{
			return false;
		}
		num += num3;
		if (num >= packet.Length)
		{
			return false;
		}
		byte b = packet[num];
		num++;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		ushort num4 = (ushort)((packet[num] & 0xFF) | ((packet[num + 1] & 0xFF) << 8));
		num += 2;
		int num5 = 0;
		int num6 = -1;
		int[] array = new int[2]
		{
			num + 2,
			num
		};
		foreach (int num7 in array)
		{
			if (num7 + 5 < packet.Length)
			{
				int num8 = (packet[num7] & 0xFF) | ((packet[num7 + 1] & 0xFF) << 8) | ((packet[num7 + 2] & 0xFF) << 16) | ((packet[num7 + 3] & 0xFF) << 24);
				if (num8 >= 1000000 && num8 <= 10000000 && packet[num7 + 5] == 2)
				{
					num5 = num8;
					num6 = num7;
					break;
				}
			}
		}
		if (num6 < 0)
		{
			return false;
		}
		entityId = num2;
		flag1 = b;
		flags = num4;
		npcId = num5;
		markerOffset = num6 + 4;
		return true;
	}

	private bool TryParseCombatActionPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 43 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (_bossTarget <= 0 || num2 != _bossTarget)
		{
			return false;
		}
		int num4 = -1;
		for (int i = num; i < packet.Length - 15; i++)
		{
			if (packet[i] == 0 && packet[i + 1] == 0 && packet[i + 2] == 0 && packet[i + 3] == 0 && packet[i + 4] == 0 && packet[i + 5] == 0)
			{
				num4 = i;
				break;
			}
		}
		if (num4 < 0)
		{
			return false;
		}
		Span<byte> span = packet.AsSpan(num4 + 6);
		for (int j = 0; j < span.Length - 8; j++)
		{
			if (span[j] != 0 || span[j + 1] != 0)
			{
				continue;
			}
			int num5 = j + 2;
			if (num5 >= span.Length)
			{
				break;
			}
			var (num6, num7) = ReadVarInt(packet, num4 + 6 + num5);
			if (num7 > 0 && num6 > 0)
			{
				int num8 = num4 + 6 + num5 + num7 + 1;
				if (num8 + 4 > packet.Length)
				{
					break;
				}
				int num9 = ParseUInt32LE(packet, num8);
				int num10 = num9 - num9 % 10000;
				if (num10 < 3000000 || (num10 > 5000000 && num10 < 11000000) || num10 > 20000000)
				{
					break;
				}
				DamageEvent e = new DamageEvent
				{
					Timestamp = NowMs(),
					ActorId = num6,
					TargetId = num2,
					Damage = 1,
					SkillCode = num10,
					RawSkillCode = num9,
					Type = 0,
					IsDoT = false,
					IsPassive = true
				};
				_logger.LogTrace("⚔\ufe0f 2B 38 combat action: Actor={Actor}, Target={Target}, Skill={Skill}", num6, num2, num10);
				DamageEventParsed?.Invoke(this, e);
				return true;
			}
		}
		return false;
	}

	private bool TryParsePlayerInfoPacket(byte[] packet)
	{
		if (!TryDecodePlayerInfo(packet, out int entityId, out string name))
		{
			return false;
		}
		_logger.LogTrace("\ud83d\udc64 44 36 player info: Entity={Entity}, Name=\"{Name}\"", entityId, name);
		try
		{
			_dataRepository.SetNickname(entityId, name, isConfirmedPlayer: true, isAuthoritativeSource: true);
			NicknameDiscovered?.Invoke(this, (entityId, name, 0, true));
			ApplyPendingPlayerStats(entityId, name);
			if (TryReadAbyssGrade(packet, out var grade))
			{
				_logger.LogTrace("\ud83c\udfc5 Abyss grade: Entity={Entity}, Name=\"{Name}\", Grade={Grade} ({Rank})", entityId, name, grade, AbyssRank.GetName(grade));
				AbyssGradeDiscovered?.Invoke(this, (entityId, grade));
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static bool TryDecodePlayerInfo(byte[] packet, out int entityId, out string name)
	{
		entityId = 0;
		name = "";
		int num = 0;
		int item = ReadVarIntStatic(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if ((packet[num] != 68 && packet[num] != 69) || packet[num + 1] != 54)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarIntStatic(packet, num);
		if (num3 < 0 || num2 <= 0)
		{
			return false;
		}
		num += num3;
		int num4 = Math.Min(num + 20, packet.Length - 2);
		for (int i = num; i < num4; i++)
		{
			if (packet[i] != 7 && packet[i] != 23)
			{
				continue;
			}
			byte b = packet[i + 1];
			if (b < 1 || b > 40 || i + 2 + b > packet.Length || (b == 1 && i != num + 4))
			{
				continue;
			}
			try
			{
				string text = Encoding.UTF8.GetString(packet, i + 2, b);
				bool flag = true;
				string text2 = text;
				foreach (char c in text2)
				{
					if (char.IsControl(c) || c == '\ufffd')
					{
						flag = false;
						break;
					}
				}
				if (!flag || text.Length < 1 || !PlayerNameValidator.IsPlausible(text))
				{
					continue;
				}
				entityId = num2;
				name = text;
				return true;
			}
			catch
			{
			}
		}
		return false;
	}

	private static bool TryReadAbyssGrade(byte[] packet, out int grade)
	{
		grade = 0;
		if (packet.Length < 41)
		{
			return false;
		}
		int num = packet.Length - 1;
		while (num >= 0 && packet[num] == 0)
		{
			num--;
		}
		int num2 = num;
		while (num2 >= 0)
		{
			if (packet[num2] != 0)
			{
				num2--;
				continue;
			}
			int num3 = num2;
			while (num3 >= 0 && packet[num3] == 0)
			{
				num3--;
			}
			if (num2 - num3 >= 25)
			{
				int num4 = num2 + 1;
				if (num4 < packet.Length && packet[num4] <= 3)
				{
					num4++;
				}
				if (num4 + 12 > packet.Length)
				{
					return false;
				}
				int num5 = packet[num4 + 8] | (packet[num4 + 9] << 8) | (packet[num4 + 10] << 16) | (packet[num4 + 11] << 24);
				if (!AbyssRank.IsValidGrade(num5))
				{
					return false;
				}
				grade = num5;
				return true;
			}
			num2 = num3;
		}
		return false;
	}

	private static bool IsLocalRecordNameMarker(byte b)
	{
		if (b == 7 || b == 55 || b == 63)
		{
			return true;
		}
		return false;
	}

	private static bool IsTrustedLocalRecordName(string name, byte[] data, int serverIdOffset)
	{
		if (name.Length <= 1)
		{
			return HasKnownServerIdAt(data, serverIdOffset);
		}
		return true;
	}

	private static bool IsTrustedLocalRecordEntity(long entityId, byte[] data, int serverIdOffset)
	{
		if (entityId < 100)
		{
			return HasKnownServerIdAt(data, serverIdOffset);
		}
		return true;
	}

	private static bool HasKnownServerIdAt(byte[] data, int offset)
	{
		if (offset >= 0 && offset + 2 <= data.Length)
		{
			return ServerIdSpace.IsKnownServerId(data[offset] | (data[offset + 1] << 8));
		}
		return false;
	}

	private void TryScanLocalPlayerRecord(byte[] data)
	{
		if (data.Length < 16)
		{
			return;
		}
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		byte[] array = (string.IsNullOrEmpty(localPlayerName) ? null : Encoding.UTF8.GetBytes(localPlayerName));
		int num = data.Length;
		for (int i = 0; i < num - 14; i++)
		{
			if (data[i] != 51 || data[i + 1] != 54)
			{
				continue;
			}
			var (num2, num3) = ReadVarInt(data, i + 2);
			if (num3 <= 0 || num3 > 3 || num2 < 1 || num2 > 999999)
			{
				continue;
			}
			int num4 = i + 2 + num3 + 4;
			if (num4 + 1 >= num || !IsLocalRecordNameMarker(data[num4]))
			{
				continue;
			}
			byte b = data[num4 + 1];
			if (b < 1 || b > 30)
			{
				continue;
			}
			int num5 = num4 + 2;
			if (num5 + b > num)
			{
				continue;
			}
			string name;
			if (array != null && b == array.Length)
			{
				bool flag = true;
				for (int j = 0; j < b; j++)
				{
					if (data[num5 + j] != array[j])
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					name = localPlayerName;
				}
				else if (!TryReadAndValidateName(data, num5, b, out name))
				{
					continue;
				}
			}
			else if (!TryReadAndValidateName(data, num5, b, out name))
			{
				continue;
			}
			if (!IsTrustedLocalRecordName(name, data, num5 + b) || !IsTrustedLocalRecordEntity(num2, data, num5 + b))
			{
				continue;
			}
			_dataRepository.SetLocalPlayerName(name);
			_logger.LogInformation("\ud83c\udfae LOCAL PLAYER ID RESOLVED (33 36 record): EntityId={EntityId}, Name='{Name}' (universal scanner)", num2, name);
			_dataRepository.SetNickname(num2, name, isConfirmedPlayer: true, isAuthoritativeSource: true);
			int? bindServerId = null;
			int num6 = num5 + b;
			if (num6 + 2 <= num)
			{
				int num7 = data[num6] | (data[num6 + 1] << 8);
				if (ServerIdSpace.IsKnownServerId(num7))
				{
					int? currentServerId = _dataRepository.GetCurrentServerId();
					if (currentServerId.HasValue)
					{
						int valueOrDefault = currentServerId.GetValueOrDefault();
						if (valueOrDefault != num7)
						{
							_logger.LogInformation("\ud83c\udf10 LOCAL PLAYER server switch (33 36): {Old} → {New} — stats carried from {Old} are not applied to '{Name}'", valueOrDefault, num7, valueOrDefault, name);
						}
					}
					_dataRepository.SetCurrentServerId(num7);
					bindServerId = num7;
					_logger.LogInformation("\ud83c\udf10 LOCAL PLAYER server ID resolved (33 36 trailing u16): {ServerId}", num7);
				}
				else
				{
					_logger.LogDebug("33 36 trailing u16 = {Value} not in production server range — ignored", num7);
				}
			}
			ApplyPendingPlayerStats(num2, name, bindServerId);
			ApplyPendingCp(num2);
			ApplyPendingLocalCp(num2);
			LocalPlayerDiscovered?.Invoke(this, (num2, name));
			if (bindServerId.HasValue)
			{
				int valueOrDefault2 = bindServerId.GetValueOrDefault();
				_dataRepository.ConfirmServerClockServer(valueOrDefault2, NowMs());
				LocalServerIdResolved?.Invoke(this, valueOrDefault2);
			}
			break;
		}
	}

	private static bool TryReadAndValidateName(byte[] data, int offset, int length, out string? name)
	{
		name = null;
		try
		{
			string text = Encoding.UTF8.GetString(data, offset, length);
			if (text.Length == 0)
			{
				return false;
			}
			bool flag = false;
			string text2 = text;
			foreach (char c in text2)
			{
				if (char.IsControl(c))
				{
					return false;
				}
				if (c == '\ufffd')
				{
					return false;
				}
				if (char.IsLetter(c))
				{
					flag = true;
				}
			}
			if (!flag)
			{
				return false;
			}
			if (!PlayerNameValidator.IsPlausible(text))
			{
				return false;
			}
			name = text;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void ScanForPlayerStats(byte[] packet)
	{
		if (packet.Length < 18)
		{
			return;
		}
		TryScanAsmodianIdentityRecord(packet);
		for (int i = 0; i < packet.Length - 12; i++)
		{
			if (packet[i] == 51 && packet[i + 1] == 54)
			{
				int entityId;
				string name;
				int serverId;
				int block;
				int level;
				int gs;
				switch (TryReadLocalRecordStats(packet, i, out entityId, out name, out serverId, out block, out level, out gs))
				{
				case LocalRecordStats.BlockMiss:
					_protocolHealth?.RecordDecodeMiss("level_gs", packet.AsSpan(block));
					break;
				default:
					_protocolHealth?.RecordDecodeOk("level_gs");
					ApplyStats(name, gs, level, serverId);
					return;
				case LocalRecordStats.NotARecord:
					break;
				}
			}
		}
	}

	private static LocalRecordStats TryReadLocalRecordStats(byte[] packet, int i, out int entityId, out string? name, out int serverId, out int block, out int level, out int gs)
	{
		entityId = 0;
		name = null;
		serverId = 0;
		block = 0;
		level = 0;
		gs = 0;
		if (i < 0 || i + 1 >= packet.Length || packet[i] != 51 || packet[i + 1] != 54)
		{
			return LocalRecordStats.NotARecord;
		}
		var (num, num2) = ReadVarIntStatic(packet, i + 2);
		if (num2 <= 0 || num2 > 3)
		{
			return LocalRecordStats.NotARecord;
		}
		if (num < 1 || num > 999999)
		{
			return LocalRecordStats.NotARecord;
		}
		int num3 = i + 2 + num2 + 4;
		if (num3 + 1 >= packet.Length)
		{
			return LocalRecordStats.NotARecord;
		}
		if (!IsLocalRecordNameMarker(packet[num3]))
		{
			return LocalRecordStats.NotARecord;
		}
		byte b = packet[num3 + 1];
		if (b < 1 || b > 30)
		{
			return LocalRecordStats.NotARecord;
		}
		int num4 = num3 + 2;
		if (num4 + b > packet.Length)
		{
			return LocalRecordStats.NotARecord;
		}
		if (!TryReadAndValidateName(packet, num4, b, out name) || name == null)
		{
			return LocalRecordStats.NotARecord;
		}
		if (!IsTrustedLocalRecordName(name, packet, num4 + b) || !IsTrustedLocalRecordEntity(num, packet, num4 + b))
		{
			name = null;
			return LocalRecordStats.NotARecord;
		}
		block = num4 + b;
		if (block + 2 > packet.Length)
		{
			return LocalRecordStats.NotARecord;
		}
		serverId = packet[block] | (packet[block + 1] << 8);
		entityId = num;
		if (!TryParse3336StatBlock(packet, block, serverId, out level, out gs))
		{
			return LocalRecordStats.BlockMiss;
		}
		return LocalRecordStats.Decoded;
	}

	private void ApplyStats(string name, int gs, int level, int? recordServerId = null)
	{
		int? currentServerId = _dataRepository.GetCurrentServerId();
		int num;
		if (recordServerId.HasValue)
		{
			int valueOrDefault = recordServerId.GetValueOrDefault();
			if (currentServerId.HasValue)
			{
				int valueOrDefault2 = currentServerId.GetValueOrDefault();
				num = ((valueOrDefault != valueOrDefault2) ? 1 : 0);
				goto IL_0038;
			}
		}
		num = 0;
		goto IL_0038;
		IL_0038:
		bool flag = (byte)num != 0;
		int num2 = ((!flag) ? FindPlayerByName(name) : 0);
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		if (num2 > 0)
		{
			(num3, num4, num5) = _dataRepository.GetPlayerStats(num2);
		}
		int? num6 = recordServerId ?? currentServerId;
		if (_pendingPlayerStats.TryGetValue(name, out var value))
		{
			if (recordServerId.HasValue)
			{
				int valueOrDefault3 = recordServerId.GetValueOrDefault();
				int? serverId = value.ServerId;
				bool flag2;
				if (serverId.HasValue)
				{
					int valueOrDefault4 = serverId.GetValueOrDefault();
					flag2 = valueOrDefault4 == valueOrDefault3;
				}
				else
				{
					flag2 = !flag;
				}
				if (!flag2)
				{
					goto IL_0128;
				}
			}
			if (num3 == 0)
			{
				num3 = value.GS;
			}
			if (num4 == 0)
			{
				num4 = value.CP;
			}
			if (num5 == 0)
			{
				num5 = value.Level;
			}
			if (!recordServerId.HasValue)
			{
				num6 = value.ServerId ?? num6;
			}
		}
		goto IL_0128;
		IL_0128:
		int num7 = ((gs > 0) ? gs : num3);
		int level2 = ((level > 0) ? level : num5);
		_pendingPlayerStats[name] = new PendingStats(num7, num4, level2, num6);
		if (num2 > 0)
		{
			_dataRepository.SetPlayerStats(num2, num7, num4, level2);
		}
	}

	private void TryScanAsmodianIdentityRecord(byte[] packet)
	{
		for (int i = 1; i + 8 < packet.Length; i++)
		{
			if (packet[i] != 32 || packet[i + 1] != 240 || packet[i + 2] != 2 || packet[i + 3] != 19 || packet[i + 5] != 30 || packet[i + 6] != 0 || packet[i + 7] != 98)
			{
				continue;
			}
			byte b = packet[i + 4];
			if (i + 11 >= packet.Length)
			{
				continue;
			}
			int num = BitConverter.ToInt32(packet, i + 8);
			bool flag = ((b < 1 || b > 99) ? true : false);
			if (flag || ((num < 1 || num > 99999) ? true : false))
			{
				continue;
			}
			for (int j = 1; j <= 30; j++)
			{
				int num2 = i - j - 1;
				if (num2 < 0)
				{
					break;
				}
				if (packet[num2] == j && TryReadAndValidateName(packet, num2 + 1, j, out string name) && name != null)
				{
					ApplyStats(name, num, b);
					return;
				}
			}
		}
	}

	private static bool TryParse3336StatBlock(byte[] packet, int block, int serverId, out int level, out int gs)
	{
		level = 0;
		gs = 0;
		string? text = ServerIdSpace.FactionOf(serverId);
		bool flag = text == "Elyos";
		bool flag2 = text == "Asmodian";
		if (!flag && !flag2)
		{
			return false;
		}
		if (TryScanUniversalLevelGs(packet, block, out level, out gs))
		{
			return true;
		}
		if (TryScanDoubledLevelGs(packet, block, out level, out gs))
		{
			return true;
		}
		if (flag && TryParseElyosFixedLevelGs(packet, block, out level, out gs))
		{
			return true;
		}
		if (flag2 && TryParseAsmodianTaggedGs(packet, block, out gs))
		{
			level = 0;
			return true;
		}
		return false;
	}

	private static bool TryScanUniversalLevelGs(byte[] packet, int block, out int level, out int gs)
	{
		level = 0;
		gs = 0;
		int num = block + 2;
		int num2 = Math.Min(block + 12, packet.Length - 1);
		for (int i = num; i <= num2 && i + 3 < packet.Length; i++)
		{
			if (packet[i + 1] != 0 || packet[i + 2] != 0 || packet[i + 3] != 0)
			{
				continue;
			}
			byte b = packet[i];
			if ((b < 1 || b > 99) ? true : false)
			{
				continue;
			}
			int num3 = i + 4;
			for (int j = 1; j <= 3 && num3 + j + 4 <= packet.Length; j++)
			{
				if (packet[num3 + j] == 4 && packet[num3 + j + 1] == 0 && packet[num3 + j + 2] == 0 && packet[num3 + j + 3] == 12)
				{
					int num4 = 0;
					for (int k = 0; k < j; k++)
					{
						num4 |= packet[num3 + k] << 8 * k;
					}
					if ((num4 < 1 || num4 > 99999) ? true : false)
					{
						break;
					}
					level = b;
					gs = num4;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryScanDoubledLevelGs(byte[] packet, int block, out int level, out int gs)
	{
		level = 0;
		gs = 0;
		int num = block + 2;
		int num2 = Math.Min(block + 12, packet.Length - 16);
		for (int i = num; i <= num2; i++)
		{
			uint num3 = BitConverter.ToUInt32(packet, i);
			uint num4 = BitConverter.ToUInt32(packet, i + 4);
			uint num5 = BitConverter.ToUInt32(packet, i + 8);
			uint num6 = BitConverter.ToUInt32(packet, i + 12);
			bool flag = ((num3 < 1 || num3 > 99) ? true : false);
			if (!flag && num3 == num6)
			{
				flag = ((num4 < 1 || num4 > 99999) ? true : false);
				if (!flag && num4 == num5)
				{
					level = (int)num3;
					gs = (int)num4;
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryParseElyosFixedLevelGs(byte[] packet, int block, out int level, out int gs)
	{
		level = 0;
		gs = 0;
		if (block + 20 > packet.Length)
		{
			return false;
		}
		int num = BitConverter.ToInt32(packet, block + 7);
		int num2 = BitConverter.ToInt32(packet, block + 15);
		if (packet[block + 19] != 12)
		{
			return false;
		}
		if ((num < 1 || num > 99) ? true : false)
		{
			return false;
		}
		if ((num2 < 1 || num2 > 99999) ? true : false)
		{
			return false;
		}
		level = num;
		gs = num2;
		return true;
	}

	private static bool TryParseAsmodianTaggedGs(byte[] packet, int block, out int gs)
	{
		gs = 0;
		int num = block + 2;
		if (num + 5 > packet.Length)
		{
			return false;
		}
		if (packet[num + 1] != 0 || packet[num + 2] != 0 || packet[num + 3] != 0 || packet[num + 4] != 2)
		{
			return false;
		}
		num += 5;
		for (int i = num; i <= num + 4 && i + 1 < packet.Length; i++)
		{
			int num2;
			if (packet[i] == 19)
			{
				num2 = 1;
			}
			else
			{
				if (packet[i] != 34)
				{
					continue;
				}
				num2 = 2;
			}
			int num3 = i + 1;
			if (num3 + num2 + 4 <= packet.Length && packet[num3 + num2] == 4 && packet[num3 + num2 + 1] == 0 && packet[num3 + num2 + 2] == 0 && packet[num3 + num2 + 3] == 12)
			{
				int num4 = 0;
				for (int j = 0; j < num2; j++)
				{
					num4 |= packet[num3 + j] << 8 * j;
				}
				if (num4 >= 1 && num4 <= 99999)
				{
					gs = num4;
					return true;
				}
			}
		}
		return false;
	}

	private static (int Value, int Length) ReadVarIntStatic(byte[] bytes, int offset)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		do
		{
			if (offset + num3 >= bytes.Length)
			{
				return (Value: -1, Length: -1);
			}
			int num4 = bytes[offset + num3] & 0xFF;
			num3++;
			num |= (num4 & 0x7F) << num2;
			if ((num4 & 0x80) == 0)
			{
				return (Value: num, Length: num3);
			}
			num2 += 7;
		}
		while (num2 < 32);
		return (Value: -1, Length: -1);
	}

	private bool IsPlausibleCp(int cp, int gs, string source, ReadOnlySpan<byte> context)
	{
		if ((cp < 1000 || cp > 3000000) ? true : false)
		{
			RejectCp(cp, gs, source, "range", context);
			return false;
		}
		if (gs > 0)
		{
			double num = (double)cp / (double)gs;
			if (num < 15.0 || num > 400.0)
			{
				RejectCp(cp, gs, source, "ratio", context);
				return false;
			}
		}
		return true;
	}

	private void RejectCp(int cp, int gs, string source, string reason, ReadOnlySpan<byte> context)
	{
		_protocolHealth?.RecordDecodeMiss("combat_power_reject", context);
		long tickCount = Environment.TickCount64;
		if (tickCount - _lastCpRejectLogTicks >= 30000)
		{
			_lastCpRejectLogTicks = tickCount;
			_logger.LogWarning("⚠\ufe0f Implausible Combat-Power rejected ({Reason}) from {Source}: cp={Cp:N0}, gs={Gs}, ctx=[{Ctx}]", reason, source, cp, gs, (context.Length > 0) ? Convert.ToHexString(context) : "");
		}
	}

	private void ScanForCombatPower(byte[] packet)
	{
		if (packet.Length < 24)
		{
			return;
		}
		for (int i = 0; i + 21 < packet.Length; i++)
		{
			if (packet[i] != 86 || packet[i + 1] != 54)
			{
				continue;
			}
			int num = i + 2 + 16 + 1;
			if (num + 2 >= packet.Length || packet[num] != 0 || packet[num + 1] != 141 || packet[i + 6] != 0 || packet[i + 7] != 0 || packet[i + 8] != 0 || packet[i + 9] != 0 || packet[i + 14] != 0 || packet[i + 15] != 0 || packet[i + 16] != 0 || packet[i + 17] != 0)
			{
				continue;
			}
			int num2 = BitConverter.ToInt32(packet, i + 10);
			(int Value, int Length) tuple = ReadVarInt(packet, num + 2);
			int item = tuple.Value;
			int item2 = tuple.Length;
			bool flag = ((num2 < 1 || num2 > 999999999) ? true : false);
			if (flag || item2 <= 0 || item < 100 || item > 999999)
			{
				int num3 = Math.Min(num + 2, packet.Length);
				_protocolHealth?.RecordDecodeMiss("combat_power", packet.AsSpan(i + 2, num3 - (i + 2)));
				continue;
			}
			int item3 = _dataRepository.GetPlayerStats(item).GearScore;
			if (IsPlausibleCp(num2, item3, "56 36 raw", packet.AsSpan(i + 2, Math.Min(16, packet.Length - (i + 2)))))
			{
				_protocolHealth?.RecordDecodeOk("combat_power");
				long num4 = NowMs();
				_pendingCpByEntity[item] = (num2, num4);
				if (_dataRepository.IsConfirmedPlayer(item))
				{
					var (gearScore, _, level) = _dataRepository.GetPlayerStats(item);
					_dataRepository.SetPlayerStats(item, gearScore, num2, level);
					_dataRepository.MarkCombatPowerObserved(item, num4, CombatPowerSource.ChangeEvent, _dataRepository.GetNickname(item));
				}
				break;
			}
		}
	}

	private void ScanForRosterCp(byte[] packet)
	{
		if (packet == null || packet.Length < 40)
		{
			return;
		}
		for (int i = 0; i + 2 < packet.Length; i++)
		{
			if (packet[i] == 2 && packet[i + 1] == 151)
			{
				int end = Math.Min(packet.Length, i + 2 + 400);
				ParseRosterMembers(packet, i + 2, end);
				break;
			}
		}
	}

	private void ScanForRoomRosters(byte[] data)
	{
		if (data == null || data.Length < 40)
		{
			return;
		}
		try
		{
			ReadOnlySpan<byte> value = new byte[2] { 2, 151 };
			Span<byte> span = data.AsSpan();
			int num = 1;
			while (num < span.Length - 1)
			{
				ref Span<byte> reference = ref span;
				int num2 = num;
				int num3 = reference.Slice(num2, reference.Length - num2).IndexOf(value);
				if (num3 < 0)
				{
					break;
				}
				int num4 = num + num3;
				int num5 = num4 + 1;
				for (int i = 1; i <= 3 && num4 - i >= 0; i++)
				{
					reference = ref span;
					num2 = num4 - i;
					if (PartyRoomRosterDecoder.TryDecode(reference.Slice(num2, reference.Length - num2), out PartyRoomRoster roster, out int recordLength))
					{
						Interlocked.Increment(ref _roomRosterPlainRecords);
						EmitRoomRoster(roster, "plain");
						num5 = num4 - i + recordLength;
						break;
					}
				}
				num = num5;
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Room roster scan error");
		}
	}

	private void HandleRoomRosterRecord(byte[] record)
	{
		if (!PartyRoomRosterDecoder.TryDecode(record, out PartyRoomRoster roster))
		{
			Interlocked.Increment(ref _roomRosterLaneRejected);
			return;
		}
		Interlocked.Increment(ref _roomRosterLaneRecords);
		EmitRoomRoster(roster, "bundle");
	}

	private void EmitRoomRoster(PartyRoomRoster roster, string source)
	{
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		if (!string.IsNullOrEmpty(localPlayerName) && (object)roster.FindCharacter(localPlayerName, _dataRepository.GetCurrentServerId()) == null)
		{
			Interlocked.Increment(ref _roomRosterWithoutLocal);
			_logger.LogDebug("\ud83c\udf8e Room roster {Room} without the local player '{Name}' dropped ({Source})", roster.RoomId, localPlayerName, source);
			return;
		}
		long num = NowMs();
		bool flag;
		lock (_roomRosterGate)
		{
			if (!_roomRosterDedup.ShouldEmit(roster, num))
			{
				Interlocked.Increment(ref _roomRosterDuplicates);
				return;
			}
			flag = _roomRosterLastLoggedRoom != roster.RoomId;
			_roomRosterLastLoggedRoom = roster.RoomId;
		}
		Interlocked.Increment(ref _roomRosterEmitted);
		if (flag)
		{
			_logger.LogInformation("\ud83c\udf8e Party room {Room} '{Title}' {Count}/{Max}, dungeon {Dungeon} ({Source})", roster.RoomId, roster.Title, roster.Members.Count, roster.MaxMembers, roster.DungeonId, source);
		}
		else
		{
			_logger.LogDebug("\ud83c\udf8e Party room {Room} updated: {Count}/{Max} ({Source})", roster.RoomId, roster.Members.Count, roster.MaxMembers, source);
		}
		PartyRoomRosterParsed?.Invoke(this, new PartyRoomRosterEvent(roster, num));
	}

	public PartyRoomRosterStats GetPartyRoomRosterStats()
	{
		return new PartyRoomRosterStats(Interlocked.Read(in _roomRosterPlainRecords), Interlocked.Read(in _roomRosterLaneRecords), Interlocked.Read(in _roomRosterLaneRejected), Interlocked.Read(in _roomRosterWithoutLocal), Interlocked.Read(in _roomRosterDuplicates), Interlocked.Read(in _roomRosterEmitted));
	}

	private void ParseRosterMembers(byte[] p, int start, int end)
	{
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		int? currentServerId = _dataRepository.GetCurrentServerId();
		IReadOnlyDictionary<int, string> readOnlyDictionary = null;
		for (int i = Math.Max(start, 2); i + 25 < end; i++)
		{
			byte b = p[i];
			if ((b < 2 || b > 30) ? true : false)
			{
				continue;
			}
			int num = i + 1;
			int num2 = num + b;
			if (num2 + 21 > end)
			{
				continue;
			}
			string text;
			try
			{
				text = Encoding.UTF8.GetString(p, num, b);
			}
			catch
			{
				continue;
			}
			if (!PlayerNameValidator.IsPlausible(text) || !TryDecodeMemberEntry(p, i, num2, end, out var leadSrv, out var level, out var gs, out var cp))
			{
				continue;
			}
			string value = null;
			if (i - 19 >= start)
			{
				_legionNames.TryGetValue(BitConverter.ToUInt32(p, i - 19), out value);
			}
			int length = Math.Min(12, end - (num2 + 8));
			if (!IsPlausibleCp(cp, gs, "02 97 roster", p.AsSpan(num2 + 8, length)))
			{
				continue;
			}
			_protocolHealth?.RecordDecodeOk("combat_power");
			if (!string.IsNullOrEmpty(localPlayerName) && text == localPlayerName && (!currentServerId.HasValue || currentServerId == leadSrv))
			{
				TryApplyLocalCp(cp);
				if (value != null)
				{
					int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
					if (localPlayerEntityId.HasValue)
					{
						_dataRepository.SetLegion(localPlayerEntityId.Value, value);
					}
				}
				continue;
			}
			if (readOnlyDictionary == null)
			{
				readOnlyDictionary = _dataRepository.GetAllNicknames();
			}
			foreach (KeyValuePair<int, string> item in readOnlyDictionary)
			{
				if (item.Value != text)
				{
					continue;
				}
				int key = item.Key;
				if (_dataRepository.IsConfirmedPlayer(key) && (!_playerServerIdCache.TryGetValue(key, out var value2) || value2 == leadSrv))
				{
					var (num3, _, num4) = _dataRepository.GetPlayerStats(key);
					_dataRepository.SetPlayerStats(key, (num3 > 0) ? num3 : gs, cp, (num4 > 0) ? num4 : level);
					_dataRepository.MarkCombatPowerObserved(key, NowMs(), CombatPowerSource.Roster, text);
					if (value != null)
					{
						_dataRepository.SetLegion(key, value);
					}
				}
			}
		}
	}

	private static bool TryDecodeMemberEntry(byte[] p, int L, int nameEnd, int end, out int leadSrv, out int level, out int gs, out int cp)
	{
		leadSrv = p[L - 2] | (p[L - 1] << 8);
		level = BitConverter.ToInt32(p, nameEnd + 4);
		gs = BitConverter.ToInt32(p, nameEnd + 8);
		cp = -1;
		int num = level;
		if ((num < 1 || num > 99) ? true : false)
		{
			return false;
		}
		num = gs;
		if ((num < 1 || num > 99999) ? true : false)
		{
			return false;
		}
		for (int i = nameEnd + 12; i <= nameEnd + 14 && i + 11 <= end; i++)
		{
			if ((p[i] | (p[i + 1] << 8)) == leadSrv)
			{
				int num2 = BitConverter.ToInt32(p, i + 5);
				if (p[i + 9] == 0 && p[i + 10] == 0)
				{
					cp = num2;
					break;
				}
			}
		}
		return cp >= 0;
	}

	private void ApplyPendingPlayerStats(int entityId, string name, int? bindServerId = null)
	{
		if (!_pendingPlayerStats.TryRemove(name, out var value))
		{
			return;
		}
		if (bindServerId.HasValue)
		{
			int valueOrDefault = bindServerId.GetValueOrDefault();
			int? serverId = value.ServerId;
			if (serverId.HasValue)
			{
				int valueOrDefault2 = serverId.GetValueOrDefault();
				if (valueOrDefault2 != valueOrDefault)
				{
					_logger.LogInformation("Pending stats for '{Name}' were observed on server {Seen}, not {Bind} — dropped (GS={GS}, CP={CP}, Level={Level})", name, valueOrDefault2, valueOrDefault, value.GS, value.CP, value.Level);
					return;
				}
			}
		}
		(int GearScore, int CombatPower, int Level) playerStats = _dataRepository.GetPlayerStats(entityId);
		int item = playerStats.GearScore;
		int item2 = playerStats.CombatPower;
		int item3 = playerStats.Level;
		int gearScore = ((value.GS > 0) ? value.GS : item);
		int combatPower = ((value.CP > 0) ? value.CP : item2);
		int level = ((value.Level > 0) ? value.Level : item3);
		_dataRepository.SetPlayerStats(entityId, gearScore, combatPower, level);
	}

	private void ApplyPendingCp(int entityId)
	{
		if (_pendingCpByEntity.TryRemove(entityId, out (int, long) value))
		{
			var (num, _) = value;
			var (num2, _, level) = _dataRepository.GetPlayerStats(entityId);
			if (IsPlausibleCp(num, num2, "56 36 pending", default))
			{
				_dataRepository.SetPlayerStats(entityId, num2, num, level);
				_dataRepository.MarkCombatPowerObserved(entityId, value.Item2, CombatPowerSource.ChangeEvent, _dataRepository.GetNickname(entityId));
			}
		}
	}

	private void ApplyPendingLocalCp(int entityId)
	{
		int pendingLocalCp = _pendingLocalCp;
		if (pendingLocalCp > 0)
		{
			_pendingLocalCp = 0;
			var (num, _, level) = _dataRepository.GetPlayerStats(entityId);
			if (IsPlausibleCp(pendingLocalCp, num, "local pending", default))
			{
				_dataRepository.SetPlayerStats(entityId, num, pendingLocalCp, level);
				_dataRepository.MarkCombatPowerObserved(entityId, _pendingLocalCpObservedMs, CombatPowerSource.Self, _dataRepository.GetLocalPlayerName());
			}
		}
	}

	private void TryApplyLocalCp(int cp)
	{
		int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
		int gs = (localPlayerEntityId.HasValue ? _dataRepository.GetPlayerStats(localPlayerEntityId.Value).GearScore : 0);
		if (IsPlausibleCp(cp, gs, "local", default))
		{
			if (localPlayerEntityId.HasValue && _dataRepository.IsConfirmedPlayer(localPlayerEntityId.Value))
			{
				var (gearScore, _, level) = _dataRepository.GetPlayerStats(localPlayerEntityId.Value);
				_dataRepository.SetPlayerStats(localPlayerEntityId.Value, gearScore, cp, level);
				_dataRepository.MarkCombatPowerObserved(localPlayerEntityId.Value, NowMs(), CombatPowerSource.Self, _dataRepository.GetLocalPlayerName());
				_pendingLocalCp = 0;
			}
			else
			{
				_pendingLocalCp = cp;
				_pendingLocalCpObservedMs = NowMs();
			}
		}
	}

	private void ScanForCombatPowerBundle(byte[] data)
	{
		if (data == null || data.Length < 11)
		{
			return;
		}
		for (int i = 1; i + 9 < data.Length; i++)
		{
			if (data[i] != 86 || data[i + 1] != 54)
			{
				continue;
			}
			byte b = data[i - 1];
			bool flag = ((b < 16 || b > 47) ? true : false);
			if (flag || data[i + 6] != 0 || data[i + 7] != 0 || data[i + 8] != 0 || data[i + 9] != 0 || (i + 17 < data.Length && (data[i + 14] != 0 || data[i + 15] != 0 || data[i + 16] != 0 || data[i + 17] != 0)))
			{
				continue;
			}
			int num = BitConverter.ToInt32(data, i + 2);
			int num2 = BitConverter.ToInt32(data, i + 10);
			if (num != 0 || num2 != 0)
			{
				if ((num2 >= 1 && num2 <= 99999999) || 1 == 0)
				{
					_protocolHealth?.RecordDecodeOk("combat_power");
					TryApplyLocalCp(num2);
					_localCpFromFramedRecord = true;
					break;
				}
				_protocolHealth?.RecordDecodeMiss("combat_power", data.AsSpan(i + 2, Math.Min(16, data.Length - (i + 2))));
			}
		}
		ScanForNameTaggedLocalCp(data);
	}

	private void ScanForNameTaggedLocalCp(byte[] data)
	{
		if (_localCpFromFramedRecord)
		{
			return;
		}
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		if (string.IsNullOrEmpty(localPlayerName))
		{
			return;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(localPlayerName);
		int num = bytes.Length;
		if ((num < 2 || num > 30) ? true : false)
		{
			return;
		}
		int? currentServerId = _dataRepository.GetCurrentServerId();
		for (int i = 2; i + 1 + num + 21 <= data.Length; i++)
		{
			if (data[i] != num)
			{
				continue;
			}
			bool flag = true;
			for (int j = 0; j < num; j++)
			{
				if (data[i + 1 + j] != bytes[j])
				{
					flag = false;
					break;
				}
			}
			if (!flag)
			{
				continue;
			}
			int nameEnd = i + 1 + num;
			if (!TryDecodeMemberEntry(data, i, nameEnd, data.Length, out var leadSrv, out var _, out var _, out var cp))
			{
				continue;
			}
			if (currentServerId.HasValue)
			{
				int valueOrDefault = currentServerId.GetValueOrDefault();
				if (valueOrDefault != leadSrv)
				{
					continue;
				}
			}
			_protocolHealth?.RecordDecodeOk("combat_power");
			TryApplyLocalCp(cp);
			break;
		}
	}

	private void ScanForLegionRecord(byte[] packet)
	{
		if (packet == null || packet.Length < 44)
		{
			return;
		}
		for (int i = 0; i + 44 <= packet.Length; i++)
		{
			if (packet[i] != 43 || packet[i + 1] != 51 || packet[i + 2] != 138 || packet[i + 7] != 0 || packet[i + 8] != 0 || packet[i + 9] != 0 || packet[i + 10] != 0)
			{
				continue;
			}
			byte b = packet[i + 13];
			bool flag = ((b < 2 || b > 32) ? true : false);
			if (flag || i + 14 + b > packet.Length)
			{
				continue;
			}
			string text;
			try
			{
				text = Encoding.UTF8.GetString(packet, i + 14, b);
			}
			catch
			{
				continue;
			}
			if (PlayerNameValidator.IsPlausible(text))
			{
				uint num = BitConverter.ToUInt32(packet, i + 5);
				if (num != 0 && (_legionNames.Count < 512 || _legionNames.ContainsKey(num)))
				{
					_legionNames[num] = text;
				}
			}
		}
	}

	private int FindPlayerByName(string name)
	{
		if (_dataRepository.GetLocalPlayerName() == name)
		{
			int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
			if (localPlayerEntityId.HasValue)
			{
				return localPlayerEntityId.Value;
			}
			return -1;
		}
		foreach (var (num2, text2) in _dataRepository.GetAllNicknames())
		{
			if (text2 == name && _dataRepository.IsConfirmedPlayer(num2))
			{
				return num2;
			}
		}
		return 0;
	}

	private bool TryParse0191Packet(byte[] packet)
	{
		if (TryRaiseFieldBossList(packet, out var reject))
		{
			return true;
		}
		if (TryParseNpcBroadcastPacket(packet))
		{
			return true;
		}
		LogFieldBossListReject(reject, "router", packet);
		return false;
	}

	private bool TryRaiseFieldBossList(byte[] record, out FieldBossListReject reject)
	{
		if (!FieldBossListDecoder.TryDecode(record, out FieldBossList list, out reject) || (object)list == null)
		{
			return false;
		}
		FieldBossEntry[] array = new FieldBossEntry[list.Entries.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = list.Entries[i].Entry;
		}
		FieldBossListParsed?.Invoke(this, new FieldBossListEvent(list.MapId, array, NowMs()));
		return true;
	}

	private void LogFieldBossListReject(FieldBossListReject reject, string source, byte[] record)
	{
		bool flag = (uint)reject <= 1u;
		if (!flag && _fieldBossRejectsLogged.TryAdd(reject, 0))
		{
			_logger.LogDebug("\ud83d\uddfa\ufe0f 01 91 Field Boss list refused ({Reason}, via {Source}): {Head}", reject, source, Convert.ToHexString(record, 0, Math.Min(record.Length, 32)));
		}
	}

	private bool TryParseNpcBroadcastPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 1 || packet[num + 1] != 145)
		{
			return false;
		}
		num += 2;
		num += 7;
		if (num >= packet.Length)
		{
			return false;
		}
		byte b = packet[num];
		num++;
		if (b == 0 || b > 10)
		{
			return false;
		}
		if (num + b * 20 > packet.Length)
		{
			return false;
		}
		_broadcastNpcPositions.Clear();
		for (int i = 0; i < b; i++)
		{
			num += 4;
			int num2 = (packet[num] & 0xFF) | ((packet[num + 1] & 0xFF) << 8) | ((packet[num + 2] & 0xFF) << 16) | ((packet[num + 3] & 0xFF) << 24);
			num += 4;
			float item2 = BitConverter.ToSingle(packet, num);
			float item3 = BitConverter.ToSingle(packet, num + 4);
			float item4 = BitConverter.ToSingle(packet, num + 8);
			num += 12;
			if (num2 > 1000000 && num2 < 10000000)
			{
				_broadcastNpcPositions[num2] = (item2, item3, item4);
			}
		}
		if (_broadcastNpcPositions.Count > 0)
		{
			_logger.LogTrace("\ud83d\udce1 NPC BROADCAST (01 91): {Count} alive NPCs: [{NpcIds}]", _broadcastNpcPositions.Count, string.Join(", ", _broadcastNpcPositions.Keys));
		}
		return true;
	}

	private bool TryParseNpcPositionPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 3 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3 + 1;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num4, num5) = ReadVarInt(packet, num);
		if (num5 < 0 || num4 <= 0)
		{
			return false;
		}
		int num6 = num + num5;
		if (num6 + 23 > packet.Length)
		{
			_logger.LogTrace("\ud83c\udfaf 03 38: entity1={Entity1} stat block too short for type code", num2);
			return true;
		}
		int num7 = BitConverter.ToUInt16(packet, num6 + 21);
		bool flag = false;
		int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
		int num8;
		if (localPlayerEntityId.HasValue && localPlayerEntityId > 0)
		{
			if (num2 == localPlayerEntityId && num4 != localPlayerEntityId)
			{
				num8 = num4;
				flag = false;
				_logger.LogTrace("\ud83c\udfaf 03 38: entity1={E1} (local player), entity2={E2} (BOSS), TypeCode={TC} (SKIPPED — belongs to entity1/player)", num2, num4, num7);
			}
			else
			{
				if (num4 != localPlayerEntityId || num2 == localPlayerEntityId)
				{
					_logger.LogTrace("03 38: Neither entity matches localPlayer={LP} (entity1={E1}, entity2={E2})", localPlayerEntityId, num2, num4);
					return true;
				}
				num8 = num2;
				flag = true;
				_logger.LogTrace("\ud83c\udfaf 03 38: entity1={E1} (BOSS), entity2={E2} (local player), TypeCode={TC}", num2, num4, num7);
			}
		}
		else
		{
			bool flag2 = _dataRepository.GetNickname(num2) != null;
			bool flag3 = _dataRepository.GetNickname(num4) != null;
			if (flag2 && !flag3)
			{
				num8 = num4;
				flag = false;
				_logger.LogTrace("\ud83c\udfaf 03 38 (nickname fallback): entity1={E1} (player, has nickname), entity2={E2} (BOSS), TypeCode={TC} (SKIPPED)", num2, num4, num7);
			}
			else if (flag3 && !flag2)
			{
				num8 = num2;
				flag = true;
				_logger.LogTrace("\ud83c\udfaf 03 38 (nickname fallback): entity1={E1} (BOSS), entity2={E2} (player, has nickname), TypeCode={TC}", num2, num4, num7);
			}
			else
			{
				num8 = num2;
				flag = true;
				_logger.LogTrace("\ud83c\udfaf 03 38 (no fallback): entity1={E1}, entity2={E2}, TypeCode={TC} — firing for entity1", num2, num4, num7);
			}
		}
		_lastCombatContextEntityId = num8;
		_logger.LogTrace("\ud83c\udfaf 03 38: bossEntityId={Boss}", num8);
		if (!_discoveredEntities.ContainsKey(num8))
		{
			if (flag)
			{
				_discoveredEntities[num8] = true;
				_logger.LogTrace("\ud83c\udfaf 03 38 (fallback, no prior id): bossEntity={BossEntityId}, typeCode={TypeCode}", num8, num7);
				NpcTemplateDiscovered?.Invoke(this, (num8, num7));
			}
			else if (num6 + 21 <= packet.Length)
			{
				float num9 = BitConverter.ToSingle(packet, num6 + 17);
				int num10 = (int)Math.Round(num9);
				_discoveredEntities[num8] = true;
				_logger.LogTrace("\ud83c\udfaf 03 38 (fallback, no prior id): entity1=player, Z-key={ZKey} (Z={ZFloat:F1}) bossEntity={BossEntityId}", num10, num9, num8);
				NpcTemplateDiscovered?.Invoke(this, (num8, num10));
			}
		}
		if (_broadcastNpcPositions.Count > 0 && !_discoveredEntities.ContainsKey(num8))
		{
			List<int> list = new List<int>();
			foreach (int key in _broadcastNpcPositions.Keys)
			{
				if (!_assignedBroadcastNpcIds.Contains(key))
				{
					list.Add(key);
				}
			}
			int num11 = 0;
			if (list.Count == 1)
			{
				num11 = list[0];
				_logger.LogInformation("\ud83c\udfaf 03 38 + 01 91 fallback: entity {EntityId} → NpcId {NpcId} (only unmatched broadcast NPC)", num8, num11);
			}
			else if (list.Count > 1 && num6 + 21 <= packet.Length)
			{
				float num12 = BitConverter.ToSingle(packet, num6 + 9);
				float num13 = BitConverter.ToSingle(packet, num6 + 13);
				float num14 = BitConverter.ToSingle(packet, num6 + 17);
				float num15 = float.MaxValue;
				foreach (int item2 in list)
				{
					if (_broadcastNpcPositions.TryGetValue(item2, out (float, float, float) value))
					{
						float num16 = num12 - value.Item1;
						float num17 = num13 - value.Item2;
						float num18 = num14 - value.Item3;
						float num19 = num16 * num16 + num17 * num17 + num18 * num18;
						if (num19 < num15)
						{
							num15 = num19;
							num11 = item2;
						}
					}
				}
				if (num11 > 0)
				{
					_logger.LogInformation("\ud83c\udfaf 03 38 + 01 91 fallback: entity {EntityId} → NpcId {NpcId} (closest of {Count} unmatched, dist={Dist:F0})", num8, num11, list.Count, Math.Sqrt(num15));
				}
			}
			if (num11 > 0)
			{
				_assignedBroadcastNpcIds.Add(num11);
				_discoveredEntities[num8] = true;
				NpcTemplateDiscovered?.Invoke(this, (num8, num11));
			}
		}
		return true;
	}

	private bool TryParse1B56Packet(byte[] packet)
	{
		TryEmitWalletChange(packet, "router");
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 27 || packet[num + 1] != 86)
		{
			return false;
		}
		num += 2;
		if (num + 2 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 1 || packet[num + 1] != 1 || packet[num + 2] != 0)
		{
			return false;
		}
		num += 3;
		if (num + 3 >= packet.Length)
		{
			return false;
		}
		uint num2 = (uint)((packet[num] & 0xFF) | ((packet[num + 1] & 0xFF) << 8) | ((packet[num + 2] & 0xFF) << 16) | ((packet[num + 3] & 0xFF) << 24));
		_logger.LogTrace("\ud83c\udff7\ufe0f 1B 56: TC={TemplateCode}, entity={EntityId}", (int)num2, _lastCombatContextEntityId);
		if (_lastCombatContextEntityId <= 0)
		{
			_logger.LogTrace("\ud83c\udff7\ufe0f 1B 56: No entity context yet (_lastCombatContextEntityId={EntityId})", _lastCombatContextEntityId);
			return true;
		}
		if (_discoveredEntities.TryAdd(_lastCombatContextEntityId, value: true))
		{
			NpcTemplateDiscovered?.Invoke(this, (_lastCombatContextEntityId, (int)num2));
		}
		else
		{
			_logger.LogTrace("ℹ\ufe0f 1B 56: Entity {EntityId} already in discovered set, skipping event", _lastCombatContextEntityId);
		}
		return true;
	}

	private bool TryParseNicknamePacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 4 || packet[num + 1] != 141)
		{
			return false;
		}
		if (_logger.IsEnabled(LogLevel.Trace))
		{
			string text = BitConverter.ToString(packet).Replace("-", " ");
			_logger.LogTrace("\ud83d\udce1 04 8D ({Len}b): {Hex}", packet.Length, text);
		}
		num += 2;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 <= 0)
		{
			return false;
		}
		bool flag = num2 > 0;
		num += num3;
		num += 4;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num4, num5) = ReadVarInt(packet, num);
		if (num5 <= 0)
		{
			return false;
		}
		num += num5;
		if (num + 2 >= packet.Length)
		{
			return false;
		}
		int num6 = packet[num] | (packet[num + 1] << 8);
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		byte b = packet[num];
		if (b < 0 || b > 72)
		{
			return false;
		}
		if (b + num > packet.Length)
		{
			return false;
		}
		byte[] subArray = packet[(num + 1)..(num + b + 1)];
		string text2 = Encoding.UTF8.GetString(subArray);
		string text3 = SanitizeNickname(text2, ServerIdSpace.IsKnownServerId(num6));
		if (num6 > 0 && num4 > 0)
		{
			_playerServerIdCache[num4] = num6;
		}
		if (text3 != null)
		{
			_logger.LogInformation("✓ NICKNAME EXTRACTED (0x04 0x8d): PlayerID={PlayerId}, Nickname='{Nickname}', ServerId={ServerId} [{Stream}]", num4, text3, num6, _currentPayloadStreamLabel);
			NicknameDiscovered?.Invoke(this, (num4, text3, num6, true));
			ApplyPendingPlayerStats(num4, text3);
			if (flag)
			{
				PlayerTargetChanged?.Invoke(this, (num4, num2));
			}
			return true;
		}
		_logger.LogTrace("✗ Nickname sanitization failed (0x04 0x8d): PlayerID={PlayerId}, RawName='{RawName}'", num4, text2);
		return false;
	}

	private static string? SanitizeNickname(string nickname, bool allowOneLetter = false)
	{
		string text = nickname.Split('\0')[0].Trim();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		if (text.Any((char c) => c == '\ufffd') || text.All(char.IsControl))
		{
			return null;
		}
		if (text.Length == 1 && text[0] < '\u0080' && !allowOneLetter)
		{
			return null;
		}
		if (!PlayerNameValidator.IsPlausible(text))
		{
			return null;
		}
		return text;
	}

	private bool TryParseServerTransferPacket(byte[] packet)
	{
		if (ServerClockOffset.TryReadFrom1536(packet, out var offset))
		{
			_dataRepository.RecordServerClockReading(offset, ServerClockOffset.TryReadServerIdFrom1536(packet, out var serverId) ? new int?(serverId) : ((int?)null), NowMs());
		}
		if (TryEmitCharacterRoster(packet, "router"))
		{
			return true;
		}
		if (!TryDecodeServerTransfer(packet, out int serverId2, out string sessionPrefix))
		{
			return false;
		}
		_logger.LogInformation("\ud83d\uddfa\ufe0f Server transfer → {ServerId} (session {Session}…)", serverId2, sessionPrefix);
		ServerTransferred?.Invoke(this, new ServerTransferEvent(serverId2, NowMs()));
		return true;
	}

	private void ScanForServerClock(byte[] data)
	{
		Span<byte> span = data.AsSpan();
		int num = 0;
		while (num < span.Length)
		{
			int num2 = num;
			int num3 = span.Slice(num2, span.Length - num2).IndexOf(ServerClockOffset.Header1536);
			if (num3 < 0)
			{
				break;
			}
			int num4 = num + num3;
			if (ServerClockOffset.TryReadAt(span, num4, out var offset, out var namedServerId))
			{
				_dataRepository.RecordServerClockReading(offset, namedServerId, NowMs());
			}
			num = num4 + 1;
		}
	}

	private bool TryParseCharacterRosterPacket(byte[] packet)
	{
		return TryEmitCharacterRoster(packet, "router");
	}

	private bool TryEmitCharacterRoster(byte[] record, string source)
	{
		if (!CharacterRosterDecoder.TryDecode(record, out CharacterRoster roster) || (object)roster == null)
		{
			return false;
		}
		CharacterRoster characterRoster = ((!roster.ServerId.HasValue && roster.Kind == CharacterRosterKind.ChangeCharacter) ? roster with
		{
			ServerId = _dataRepository.GetCurrentServerId()
		} : roster);
		long num = NowMs();
		lock (_characterRosterGate)
		{
			if (!_characterRosterDedup.ShouldEmit(characterRoster, num))
			{
				return true;
			}
		}
		_logger.LogInformation("\ud83d\udc65 Character list ({Kind}, {Count} characters, server {Server}) ({Source})", characterRoster.Kind, characterRoster.Entries.Count((CharacterRosterEntry e) => !e.IsPlaceholder), characterRoster.ServerId, source);
		CharacterRosterReceived?.Invoke(this, new CharacterRosterEvent(characterRoster, num));
		return true;
	}

	private bool TryParseServerClockOffsetPacket(byte[] packet)
	{
		if (!ServerClockOffset.TryReadFrom00E2(packet, out var offset))
		{
			return false;
		}
		_dataRepository.RecordServerClockReading(offset, null, NowMs());
		return true;
	}

	private bool TryParseDungeonRunStatePacket(byte[] packet)
	{
		if (!TryDecodeDungeonRunState(packet, out var dungeonId, out var state, out var startMs, out var endMs))
		{
			return false;
		}
		_logger.LogInformation("\ud83c\udfc1 Dungeon run state {State} dungeon={DungeonId} start={StartMs} end={EndMs}", state, dungeonId, startMs, endMs);
		DungeonRunStateChanged?.Invoke(this, new DungeonRunStateEvent(dungeonId, state, startMs, endMs, NowMs()));
		return true;
	}

	private bool TryParseDungeonRunResultPacket(byte[] packet)
	{
		if (!TryDecodeDungeonRunResult(packet, out var dungeonId, out var cleared))
		{
			return false;
		}
		_logger.LogInformation("\ud83c\udfc1 Dungeon run result dungeon={DungeonId} cleared={Cleared}", dungeonId, cleared);
		DungeonRunResultReceived?.Invoke(this, new DungeonRunResultEvent(dungeonId, cleared, NowMs()));
		return true;
	}

	private bool TryParseLootEnergyChangePacket(byte[] packet)
	{
		if (LootEnergyRecordDecoder.TryDecodeUpdateEntry(packet, out var entry))
		{
			ContentsTicketsUpdated?.Invoke(this, new ContentsTicketsEvent(new ContentsTicketEntry[1] { entry }, FromLoginList: false, NowMs()));
		}
		if (!LootEnergyRecordDecoder.TryDecodeUpdate(packet, out var reading))
		{
			return false;
		}
		_logger.LogInformation("\ud83d\udd0b Odyle energy changed: {Daily} (+{Bonus})", reading.Daily, reading.Bonus);
		LootEnergyUpdated?.Invoke(this, new LootEnergyEvent(reading.Daily, reading.Bonus, FromLoginList: false, NowMs()));
		return true;
	}

	private void ScanForLootEnergyList(byte[] data)
	{
		if (LootEnergyRecordDecoder.TryFindListInBundle(data, out var reading))
		{
			_logger.LogDebug("\ud83d\udd0b Odyle energy (login list): {Daily} (+{Bonus})", reading.Daily, reading.Bonus);
			LootEnergyUpdated?.Invoke(this, new LootEnergyEvent(reading.Daily, reading.Bonus, FromLoginList: true, NowMs()));
			if (LootEnergyRecordDecoder.TryFindListEntriesInBundle(data, out IReadOnlyList<ContentsTicketEntry> entries))
			{
				ContentsTicketsUpdated?.Invoke(this, new ContentsTicketsEvent(entries, FromLoginList: true, NowMs()));
			}
		}
	}

	private void ScanForInventoryList(byte[] data)
	{
		if (InventoryRecordDecoder.TryFindListInBundle(data, out IReadOnlyList<InventoryItem> items))
		{
			EmitWalletList(items, "router bundle");
		}
	}

	private bool TryEmitWalletList(byte[] record, string source)
	{
		if (!InventoryRecordDecoder.TryDecodeList(record, out IReadOnlyList<InventoryItem> items))
		{
			return false;
		}
		EmitWalletList(items, source);
		return true;
	}

	private void EmitWalletList(IReadOnlyList<InventoryItem> items, string source)
	{
		IReadOnlyList<WalletItem> readOnlyList = InventoryRecordDecoder.WalletOf(items);
		if (readOnlyList == null)
		{
			_logger.LogWarning("\ud83d\udcb0 Inventory list with two wallet stacks of one currency — not read ({Source})", source);
			return;
		}
		if (_logger.IsEnabled(LogLevel.Debug))
		{
			_logger.LogDebug("\ud83d\udcb0 Inventory list: {Items} stacks, wallet {Wallet} ({Source})", items.Count, string.Join(", ", readOnlyList.Select((WalletItem w) => $"{w.Currency}={w.Count}")), source);
		}
		WalletUpdated?.Invoke(this, new WalletEvent(readOnlyList, FromLoginList: true, NowMs()));
	}

	private bool TryEmitWalletChange(byte[] record, string source)
	{
		if (!InventoryRecordDecoder.TryDecodeChange(record, out IReadOnlyList<InventoryItemChange> changes))
		{
			return false;
		}
		IReadOnlyList<WalletItem> readOnlyList = InventoryRecordDecoder.WalletOf(changes);
		if (readOnlyList.Count == 0)
		{
			return true;
		}
		int length = InventoryRecordDecoder.RecordLength(record);
		long num = NowMs();
		lock (_walletChangeGate)
		{
			if (!_walletChangeDedup.ShouldEmit(record.AsSpan(0, length), num))
			{
				return true;
			}
		}
		if (_logger.IsEnabled(LogLevel.Debug))
		{
			_logger.LogDebug("\ud83d\udcb0 Wallet change: {Wallet} ({Source})", string.Join(", ", readOnlyList.Select((WalletItem w) => $"{w.Currency} {w.Op} {w.Count}")), source);
		}
		WalletUpdated?.Invoke(this, new WalletEvent(readOnlyList, FromLoginList: false, num));
		return true;
	}

	internal static bool TryDecodeDungeonRunState(byte[] packet, out int dungeonId, out int state, out long startMs, out long endMs)
	{
		dungeonId = 0;
		state = 0;
		startMs = 0L;
		endMs = 0L;
		if (packet.Length < 24 || packet[0] != 27)
		{
			return false;
		}
		if (packet[1] != 0 || packet[2] != 97)
		{
			return false;
		}
		int num = BitConverter.ToInt32(packet, 3);
		byte b = packet[7];
		long num2 = BitConverter.ToInt64(packet, 8);
		long num3 = BitConverter.ToInt64(packet, 16);
		if (num <= 0 || b < 1 || b > 9)
		{
			return false;
		}
		if (num2 < 1600000000000L || num2 > 2100000000000L || num3 < 1600000000000L || num3 > 2100000000000L || num3 < num2)
		{
			return false;
		}
		dungeonId = num;
		state = b;
		startMs = num2;
		endMs = num3;
		return true;
	}

	internal static bool TryDecodeDungeonRunResult(byte[] packet, out int dungeonId, out bool cleared)
	{
		dungeonId = 0;
		cleared = false;
		if (packet.Length < 10 || packet[0] != 13)
		{
			return false;
		}
		if (packet[1] != 1 || packet[2] != 97 || packet[3] != 0)
		{
			return false;
		}
		int num = BitConverter.ToInt32(packet, 4);
		if (num <= 0 || packet[8] > 1 || packet[9] != 0)
		{
			return false;
		}
		dungeonId = num;
		cleared = packet[8] == 1;
		return true;
	}

	internal static bool TryDecodeServerTransfer(byte[] packet, out int serverId, out string sessionPrefix)
	{
		serverId = 0;
		sessionPrefix = "";
		if (packet.Length < 83 || packet[0] != 83)
		{
			return false;
		}
		if (packet[1] != 21 || packet[2] != 54 || packet[3] != 0 || packet[4] != 0)
		{
			return false;
		}
		long num = BitConverter.ToInt64(packet, 5);
		long num2 = BitConverter.ToInt64(packet, 13);
		if (num < 1600000000000L || num > 2100000000000L)
		{
			return false;
		}
		long num3 = num2 - num;
		if (num3 % 900000 != 0L || Math.Abs(num3) > 50400000)
		{
			return false;
		}
		if (packet[21] != 0 || packet[22] != 2)
		{
			return false;
		}
		int num4 = packet[24];
		int num5 = 25 + num4;
		if (num5 + 13 != 80)
		{
			return false;
		}
		int num6 = Array.IndexOf(packet, (byte)58, 25, num4);
		if (num6 <= 25 || num6 == num5 - 1)
		{
			return false;
		}
		for (int i = 25; i < num5; i++)
		{
			byte b = packet[i];
			if (b < 33 || b > 126 || (i < num6 && !IsAsciiDigit(b)))
			{
				return false;
			}
		}
		if (packet[num5] != 4 || packet[num5 + 5] != 4)
		{
			return false;
		}
		int num7 = 0;
		for (int j = 0; j < 4; j++)
		{
			if (!IsAsciiDigit(packet[num5 + 1 + j]) || !IsAsciiDigit(packet[num5 + 6 + j]))
			{
				return false;
			}
			num7 = num7 * 10 + (packet[num5 + 1 + j] - 48);
		}
		serverId = num7;
		sessionPrefix = Encoding.ASCII.GetString(packet, 25, Math.Min(8, num4));
		return true;
		static bool IsAsciiDigit(byte b2)
		{
			if (b2 >= 48)
			{
				return b2 <= 57;
			}
			return false;
		}
	}

	private bool TryParseGameServerPacket(byte[] packet)
	{
		if (packet.Length < 15)
		{
			return false;
		}
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 22 || packet[num + 1] != 54)
		{
			return false;
		}
		num += 2;
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		byte b = packet[num];
		if (b < 7 || b > 15)
		{
			return false;
		}
		num++;
		if (num + b > packet.Length)
		{
			return false;
		}
		string text = Encoding.ASCII.GetString(packet, num, b);
		if (IPAddress.TryParse(text, out IPAddress _))
		{
			_logger.LogInformation("\ud83c\udf10 Game server discovered: {IP} [{Stream}@{Device}]", text, _currentPayloadStreamLabel, _currentPayloadDeviceName);
			_streamReassembler.Reset();
			GameServerDiscovered?.Invoke(this, text);
			return true;
		}
		return false;
	}

	private void ScanForSkillLoadout(byte[] data)
	{
		if (data.Length < 13)
		{
			return;
		}
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		Dictionary<int, (int, List<int>)> dictionary2 = new Dictionary<int, (int, List<int>)>();
		for (int i = 0; i + 13 <= data.Length; i++)
		{
			if (data[i] != 19 || data[i + 1] != 56 || data[i + 2] != 1)
			{
				continue;
			}
			int num = (int)ReadUInt32LE(data, i + 3);
			if (num >= 11000000 && num <= 19999999 && num % 10000 == 0 && data[i + 7] == 1 && ReadUInt32LE(data, i + 8) == (uint)num)
			{
				byte b = data[i + 12];
				if (b >= 1 && b <= 40)
				{
					dictionary[num] = b;
				}
			}
		}
		for (int j = 0; j + 8 <= data.Length; j++)
		{
			if (data[j] != 15 || data[j + 1] != 56 || data[j + 2] != 1)
			{
				continue;
			}
			int num2 = (int)ReadUInt32LE(data, j + 3);
			int num3 = num2 / 10000 * 10000;
			if (num3 < 11000000 || num3 > 19999999)
			{
				continue;
			}
			List<int> list = DecodeSpecSuffix(num2);
			if (list != null)
			{
				byte b2 = data[j + 7];
				if (b2 >= 1 && b2 <= 40)
				{
					dictionary2[num3] = (b2, list);
				}
			}
		}
		if (dictionary.Count == 0 && dictionary2.Count == 0)
		{
			return;
		}
		List<SkillRosterEntry> list2 = new List<SkillRosterEntry>(dictionary.Count);
		foreach (KeyValuePair<int, int> item in dictionary)
		{
			list2.Add(new SkillRosterEntry(item.Key, item.Value));
		}
		List<SkillEquippedEntry> list3 = new List<SkillEquippedEntry>(dictionary2.Count);
		foreach (KeyValuePair<int, (int, List<int>)> item2 in dictionary2)
		{
			list3.Add(new SkillEquippedEntry(item2.Key, item2.Value.Item1, item2.Value.Item2));
		}
		SkillLoadoutSnapshot e = new SkillLoadoutSnapshot(list2, list3);
		long num4 = ComputeSkillLoadoutHash(list2, list3);
		if (num4 != _lastSkillLoadoutHash)
		{
			_lastSkillLoadoutHash = num4;
			_logger.LogInformation("\ud83c\udf93 Skill loadout decoded: {Roster} roster (actives+passives), {Equipped} equipped (with specs / stigmas)", list2.Count, list3.Count);
			SkillLoadoutDecoded?.Invoke(this, e);
		}
	}

	private static List<int>? DecodeSpecSuffix(int raw)
	{
		int num = raw % 10000;
		if (num % 10 != 0)
		{
			return null;
		}
		int num2 = num / 10;
		if (num2 <= 0)
		{
			return null;
		}
		List<int> list = new List<int>(3);
		while (num2 > 0)
		{
			list.Insert(0, num2 % 10);
			num2 /= 10;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] < 1 || list[i] > 5)
			{
				return null;
			}
			if (i > 0 && list[i] <= list[i - 1])
			{
				return null;
			}
		}
		return list;
	}

	private static uint ReadUInt32LE(byte[] data, int offset)
	{
		return (uint)((data[offset] & 0xFF) | ((data[offset + 1] & 0xFF) << 8) | ((data[offset + 2] & 0xFF) << 16) | ((data[offset + 3] & 0xFF) << 24));
	}

	private static long ComputeSkillLoadoutHash(List<SkillRosterEntry> roster, List<SkillEquippedEntry> equipped)
	{
		long num = 1469598103934665603L;
		foreach (SkillRosterEntry item in roster)
		{
			num += Mix(1469598103934665603L, ((long)item.Code << 8) ^ (uint)item.Level);
		}
		foreach (SkillEquippedEntry item2 in equipped)
		{
			long num2 = ((long)item2.BaseCode << 8) ^ (uint)item2.Level;
			foreach (int spec in item2.Specs)
			{
				num2 = (num2 << 3) ^ (uint)spec ^ 0x2000000;
			}
			num += Mix(1469598103934665603L, num2);
		}
		return num;
		static long Mix(long h, long v)
		{
			return (h ^ v) * 1099511628211L;
		}
	}

	public void ProcessReassembledStreamForSeasonRecord(CapturedPayload payload)
	{
		byte[] array = payload?.Data;
		if (array == null || array.Length < 64)
		{
			return;
		}
		try
		{
			if (IndexOfBytes(array, array.Length, SeasonJsonKeyNeedle) >= 0)
			{
				ScanForAbyssSeasonRecord(array, array.Length);
				ScanCompressedBundlesForSeasonRecord(array);
			}
		}
		catch (Exception exception)
		{
			_logger.LogDebug(exception, "Abyss season scan failed on a reassembled stream buffer");
		}
	}

	private static int IndexOfBytes(byte[] data, int length, byte[] needle)
	{
		int num = length - needle.Length;
		for (int i = 0; i <= num; i++)
		{
			if (data[i] != needle[0])
			{
				continue;
			}
			bool flag = true;
			for (int j = 1; j < needle.Length; j++)
			{
				if (data[i + j] != needle[j])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return i;
			}
		}
		return -1;
	}

	private void ScanCompressedBundlesForSeasonRecord(byte[] data)
	{
		int num = 64;
		for (int i = 0; i < data.Length - 6; i++)
		{
			if (data[i] != byte.MaxValue || data[i + 1] != byte.MaxValue)
			{
				continue;
			}
			int num2 = data[i + 2] | (data[i + 3] << 8) | (data[i + 4] << 16) | (data[i + 5] << 24);
			if (num2 <= 0 || num2 > 1000000)
			{
				continue;
			}
			int num3 = i + 6;
			if (num3 < data.Length)
			{
				if (num-- <= 0)
				{
					break;
				}
				int num4 = num2 + 64;
				if (_seasonDecodeScratch.Length < num4)
				{
					_seasonDecodeScratch = new byte[num4];
				}
				byte[] seasonDecodeScratch = _seasonDecodeScratch;
				int num5;
				try
				{
					num5 = LZ4Codec.Decode(data, num3, data.Length - num3, seasonDecodeScratch, 0, num4);
				}
				catch
				{
					continue;
				}
				if (num5 > 0)
				{
					ScanForAbyssSeasonRecord(seasonDecodeScratch, num5);
				}
			}
		}
	}

	private void ScanForAbyssSeasonRecord(byte[] data, int length)
	{
		if (length > data.Length)
		{
			length = data.Length;
		}
		if (length < 64)
		{
			return;
		}
		for (int i = 0; i + SeasonJsonAnchor.Length < length; i++)
		{
			if (data[i] != SeasonJsonAnchor[0])
			{
				continue;
			}
			bool flag = true;
			for (int j = 1; j < SeasonJsonAnchor.Length; j++)
			{
				if (data[i + j] != SeasonJsonAnchor[j])
				{
					flag = false;
					break;
				}
			}
			if (flag && TryDecodeAbyssSeasonRecord(data, length, i, out AbyssSeasonRecord record))
			{
				long num = ComputeAbyssSeasonHash(record);
				if (num != _lastAbyssSeasonHash)
				{
					_lastAbyssSeasonHash = num;
					_logger.LogInformation("\ud83c\udfc5 Abyss season record decoded: '{Name}' grade {Grade} ({Rank}), {Points:N0} pts, K/D/A {K}/{D}/{A}", record.CharacterName, record.Grade, AbyssRank.GetName(record.Grade), record.RankingPoints, record.Kills, record.Deaths, record.Assists);
					AbyssSeasonRecordDiscovered?.Invoke(this, record);
				}
				break;
			}
		}
	}

	private bool TryDecodeAbyssSeasonRecord(byte[] data, int length, int jsonStart, out AbyssSeasonRecord record)
	{
		record = null;
		int num = Math.Min(length, jsonStart + 128);
		int num2 = -1;
		for (int i = jsonStart + SeasonJsonAnchor.Length; i < num; i++)
		{
			if (data[i] == 125)
			{
				num2 = i;
				break;
			}
		}
		if (num2 < 0)
		{
			return false;
		}
		int num3 = num2 - jsonStart + 1;
		if (jsonStart < 1 || data[jsonStart - 1] != num3)
		{
			return false;
		}
		int num4 = jsonStart - 10;
		if (num4 < 10)
		{
			return false;
		}
		if (data[num4] != 3)
		{
			return false;
		}
		if (ReadUInt32LE(data, jsonStart - 14) != 0)
		{
			return false;
		}
		uint num5 = ReadUInt32LE(data, jsonStart - 18);
		bool flag = ((num5 < 1 || num5 > 99) ? true : false);
		if (flag)
		{
			return false;
		}
		if (data[jsonStart - 20] != 1 || data[jsonStart - 19] != 0)
		{
			return false;
		}
		ulong num6 = ReadUInt64LE(data, jsonStart - 9);
		if (num6 > 1000000000)
		{
			return false;
		}
		int value4;
		int value5;
		int value6;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(data.AsMemory(jsonStart, num3));
			JsonElement rootElement = jsonDocument.RootElement;
			if (!rootElement.TryGetProperty("kill_count", out var value) || !rootElement.TryGetProperty("death_count", out var value2) || !rootElement.TryGetProperty("assist_count", out var value3))
			{
				flag = false;
				goto IL_02ca;
			}
			if (!value.TryGetInt32(out value4) || !value2.TryGetInt32(out value5) || !value3.TryGetInt32(out value6))
			{
				flag = false;
				goto IL_02ca;
			}
		}
		catch (JsonException)
		{
			flag = false;
			goto IL_02ca;
		}
		if ((value4 < 0 || value4 > 10000000) ? true : false)
		{
			return false;
		}
		if ((value5 < 0 || value5 > 10000000) ? true : false)
		{
			return false;
		}
		if ((value6 < 0 || value6 > 10000000) ? true : false)
		{
			return false;
		}
		int num7 = num2 + 1;
		if (num7 + 28 > length)
		{
			return false;
		}
		int rankDelta = (int)ReadUInt32LE(data, num7);
		int grade = (int)ReadUInt32LE(data, num7 + 12);
		if (!AbyssRank.IsValidGrade(grade))
		{
			_protocolHealth?.RecordDecodeMiss("abyss_season", data.AsSpan(num7, 16));
			return false;
		}
		ulong num8 = ReadUInt64LE(data, num7 + 20);
		DateTime? boardSnapshotAt = null;
		if (num8 >= 1704067200000L && num8 <= long.MaxValue)
		{
			DateTime utcDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)num8).UtcDateTime;
			if (utcDateTime <= DateTimeOffset.FromUnixTimeMilliseconds(NowMs()).UtcDateTime.AddDays(7.0))
			{
				boardSnapshotAt = utcDateTime;
			}
		}
		if (!TryReadSeasonRecordName(data, jsonStart - 20, out string characterName))
		{
			return false;
		}
		_protocolHealth?.RecordDecodeOk("abyss_season");
		record = new AbyssSeasonRecord(characterName, grade, (long)num6, value4, value5, value6, rankDelta, boardSnapshotAt);
		return true;
		IL_02ca:
		return flag;
	}

	private static bool TryReadSeasonRecordName(byte[] data, int markerPos, out string characterName)
	{
		characterName = string.Empty;
		int num = markerPos - 1;
		if (num < 2)
		{
			return false;
		}
		string text = null;
		for (int i = 0; i <= 48; i++)
		{
			int num2 = num - i;
			if (num2 < 3)
			{
				break;
			}
			if (data[num2] != i || (i > 0 && !IsPrintableUtf8(data, num2 + 1, i)))
			{
				continue;
			}
			int num3 = num2 - 1;
			for (int j = 2; j <= 48; j++)
			{
				int num4 = num3 - j;
				if (num4 < 0)
				{
					break;
				}
				if (data[num4] == j && TryDecodeUtf8(data, num4 + 1, j, out string text2) && PlayerNameValidator.IsPlausible(text2))
				{
					if (text == null)
					{
						text = text2;
					}
					else if (!string.Equals(text, text2, StringComparison.Ordinal))
					{
						return false;
					}
				}
			}
		}
		if (text == null)
		{
			return false;
		}
		characterName = text;
		return true;
	}

	private static bool TryDecodeUtf8(byte[] data, int offset, int length, out string text)
	{
		text = string.Empty;
		try
		{
			text = Encoding.UTF8.GetString(data, offset, length);
		}
		catch
		{
			return false;
		}
		string text2 = text;
		foreach (char c in text2)
		{
			if (char.IsControl(c) || c == '\ufffd')
			{
				return false;
			}
		}
		return text.Length > 0;
	}

	private static bool IsPrintableUtf8(byte[] data, int offset, int length)
	{
		string text;
		return TryDecodeUtf8(data, offset, length, out text);
	}

	private static ulong ReadUInt64LE(byte[] data, int offset)
	{
		return ReadUInt32LE(data, offset) | ((ulong)ReadUInt32LE(data, offset + 4) << 32);
	}

	private static long ComputeAbyssSeasonHash(AbyssSeasonRecord r)
	{
		long h = 1469598103934665603L;
		string characterName = r.CharacterName;
		for (int i = 0; i < characterName.Length; i++)
		{
			Mix(characterName[i]);
		}
		Mix(r.Grade);
		Mix(r.RankingPoints);
		Mix(r.Kills);
		Mix(r.Deaths);
		Mix(r.Assists);
		Mix(r.RankDelta);
		Mix(r.BoardSnapshotAt?.Ticks ?? 0);
		return h;
		void Mix(long v)
		{
			h = (h ^ v) * 1099511628211L;
		}
	}

	private bool TryParseSkillSpecPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		byte b = packet[num];
		if (packet[num + 1] != 56)
		{
			return false;
		}
		if (b != 15 && b != 19)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 1)
		{
			return false;
		}
		num++;
		if (num + 4 > packet.Length)
		{
			return false;
		}
		int num2 = BitConverter.ToInt32(packet, num);
		num += 4;
		if (num2 < 11000000 || num2 > 20000000)
		{
			return false;
		}
		if (num + 1 < packet.Length && packet[num] == 5 && packet[num + 1] == 0)
		{
			return TryParseBulkSkillRoster(packet, num - 5);
		}
		int num3 = num2 / 10000 * 10000;
		int num4 = num2 % 10000 / 10;
		HashSet<int> hashSet = new HashSet<int>();
		while (num4 > 0)
		{
			int num5 = num4 % 10;
			if (num5 > 0)
			{
				hashSet.Add(num5);
			}
			num4 /= 10;
		}
		int num6 = 0;
		if (b == 19 && num + 6 < packet.Length && packet[num] == 1)
		{
			int num7 = num + 1 + 4;
			if (num7 + 1 < packet.Length)
			{
				num6 = packet[num7 + 1];
			}
		}
		_logger.LogInformation("\ud83d\udd27 SKILL SPEC ({Opcode:X2} 38): raw={RawCode}, base={BaseCode}, specs=[{Specs}], level={Level}", b, num2, num3, string.Join(", ", hashSet.OrderBy((int s) => s)), num6);
		SkillSpecsDetected?.Invoke(this, (num3, hashSet, num6));
		return true;
	}

	private bool TryParseBulkSkillRoster(byte[] packet, int startOffset)
	{
		int num = 0;
		int num2 = startOffset;
		while (num2 + 7 <= packet.Length)
		{
			byte b = packet[num2];
			if (b != 1 && b != 65 && b != 49)
			{
				num2++;
				continue;
			}
			if (num2 + 7 > packet.Length)
			{
				break;
			}
			int num3 = BitConverter.ToInt32(packet, num2 + 1);
			int num4 = num3 / 1000000;
			if (num4 < 11 || num4 > 18 || num3 % 10000 != 0)
			{
				num2++;
				continue;
			}
			if (packet[num2 + 5] != 5 || packet[num2 + 6] != 0)
			{
				num2++;
				continue;
			}
			int num5 = num2 + 7;
			var (num6, num7) = ReadVarInt(packet, num5);
			if (num7 < 0)
			{
				num2++;
				continue;
			}
			num5 += num7;
			(int Value, int Length) tuple2 = ReadVarInt(packet, num5);
			var (num8, _) = tuple2;
			if (tuple2.Length < 0)
			{
				num2++;
				continue;
			}
			_logger.LogInformation("\ud83d\udcda BULK ENTRY: code={Code}, entryType={Type}, level={Level}, marker=0x{Marker:X2}", num3, num6, num8, b);
			bool flag;
			switch (num6)
			{
			case 7:
			case 16:
			case 37:
			case 82:
			case 97:
			case 98:
			case 112:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag && num8 > 0 && num8 <= 40)
			{
				int item = num3 / 10000 * 10000;
				SkillSpecsDetected?.Invoke(this, (item, new HashSet<int>(), num8));
				num++;
			}
			num2 += 7;
		}
		if (num > 0)
		{
			_logger.LogInformation("\ud83d\udcda BULK SKILL ROSTER (13 38): extracted levels for {Count} skills from login packet", num);
		}
		return num > 0;
	}

	private static SpecialDamageFlags ParseSpecialDamageFlags(byte[] packet)
	{
		if (packet.Length == 8)
		{
			return SpecialDamageFlags.None;
		}
		if (packet.Length < 10)
		{
			return SpecialDamageFlags.None;
		}
		byte b = packet[0];
		SpecialDamageFlags specialDamageFlags = SpecialDamageFlags.None;
		if ((b & 1) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Back;
		}
		if ((b & 2) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Parry;
		}
		if ((b & 4) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Block;
		}
		if ((b & 8) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Perfect;
		}
		if ((b & 0x10) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Double;
		}
		if ((b & 0x20) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Endure;
		}
		if ((b & 0x40) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.Unknown4;
		}
		if ((b & 0x80) != 0)
		{
			specialDamageFlags |= SpecialDamageFlags.PowerShard;
		}
		return specialDamageFlags;
	}

	private (int Value, int Length) ReadVarInt(byte[] bytes, int offset = 0)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		do
		{
			if (offset + num3 >= bytes.Length)
			{
				_logger.LogTrace("VarInt read out of bounds at offset {Offset}", offset);
				return (Value: -1, Length: -1);
			}
			int num4 = bytes[offset + num3] & 0xFF;
			num3++;
			num |= (num4 & 0x7F) << num2;
			if ((num4 & 0x80) == 0)
			{
				return (Value: num, Length: num3);
			}
			num2 += 7;
		}
		while (num2 < 32);
		_logger.LogTrace("VarInt overflow at offset {Offset}", offset);
		return (Value: -1, Length: -1);
	}

	private static byte[] ConvertVarInt(int value)
	{
		List<byte> list = new List<byte>();
		int num;
		for (num = value; num > 127; num >>= 7)
		{
			list.Add((byte)((num & 0x7F) | 0x80));
		}
		list.Add((byte)num);
		return list.ToArray();
	}

	private static int ParseUInt16LE(byte[] packet, int offset = 0)
	{
		if (offset + 2 > packet.Length)
		{
			throw new ArgumentException("Packet length is shorter than required");
		}
		return (packet[offset] & 0xFF) | ((packet[offset + 1] & 0xFF) << 8);
	}

	private static int ParseUInt32LE(byte[] packet, int offset = 0)
	{
		if (offset + 4 > packet.Length)
		{
			throw new ArgumentException("Packet length is shorter than required");
		}
		return (packet[offset] & 0xFF) | ((packet[offset + 1] & 0xFF) << 8) | ((packet[offset + 2] & 0xFF) << 16) | ((packet[offset + 3] & 0xFF) << 24);
	}

	private (int skillCode, int rawSkillCode, int bytesConsumed) ReadSkillCode(byte[] packet, int offset)
	{
		if (offset + 4 > packet.Length)
		{
			return (skillCode: -1, rawSkillCode: -1, bytesConsumed: -1);
		}
		int num = ParseUInt32LE(packet, offset);
		if (_skillRepository != null && _skillRepository.HasSkill(num))
		{
			return (skillCode: num, rawSkillCode: num, bytesConsumed: 5);
		}
		return (skillCode: num - num % 10000, rawSkillCode: num, bytesConsumed: 5);
	}

	private void LogTrunc(string field, byte[] packet, int offset)
	{
		_diagTruncated++;
		string text = BitConverter.ToString((packet.Length > 60) ? packet[..60] : packet).Replace("-", " ");
		_logger.LogTrace("✂\ufe0f TRUNC at {Field}: offset={Offset}, pktLen={Len}, hex={Hex}", field, offset, packet.Length, text);
	}

	private bool TryParseDefenseEffectPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 6 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0 || num2 <= 0)
		{
			return false;
		}
		num += num3;
		if (num + 4 > packet.Length)
		{
			return false;
		}
		if (packet[num] == 225 && packet[num + 1] == 1 && packet[num + 2] == 195 && packet[num + 3] == 0)
		{
			_logger.LogTrace("\ud83d\udee1\ufe0f skip 0x0638 E1 01 C3 00 — pending investigation");
			return true;
		}
		if (packet[num] == 232 && packet[num + 1] == 239 && packet[num + 2] == 184 && packet[num + 3] == 0)
		{
			_logger.LogInformation("\ud83d\udee1\ufe0f PARRY (full-mitigation, 0-damage) — target entity={Entity}", num2);
			DefenseEventParsed?.Invoke(this, new DefenseEvent
			{
				Timestamp = NowMs(),
				ActorId = 0,
				TargetId = num2,
				SkillCode = 0,
				Outcome = DefenseOutcome.Parry
			});
			return true;
		}
		return true;
	}

	private static int AbnormalRecordEnd(byte[] packet, int declaredValue, int lengthSize)
	{
		return Math.Min(packet.Length, lengthSize + declaredValue - 4);
	}

	private bool TryParseAbnormalApplyPacket(byte[] packet)
	{
		int num = 0;
		var (declaredValue, num2) = ReadVarInt(packet, num);
		if (num2 < 0)
		{
			return false;
		}
		num += num2;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if ((packet[num] != 42 && packet[num] != 43) || packet[num + 1] != 56)
		{
			return false;
		}
		bool flag = packet[num] == 43;
		num += 2;
		int num3 = AbnormalRecordEnd(packet, declaredValue, num2);
		var (num4, num5) = ReadVarInt(packet, num);
		if (num5 < 0 || num4 <= 0)
		{
			return false;
		}
		num += num5;
		if (!flag)
		{
			if (num >= num3 || packet[num] != 1)
			{
				return false;
			}
			num++;
		}
		if (num >= num3)
		{
			return false;
		}
		if (packet[num] == 17 && TryParseBossEnrageApply(packet, num + 1, num3, num4))
		{
			return true;
		}
		if (packet[num] != 19)
		{
			return false;
		}
		num++;
		var (num6, num7) = ReadVarInt(packet, num);
		if (num7 <= 0 || num6 < 0)
		{
			return false;
		}
		num += num7;
		if (num + 4 + 4 + 4 + 8 > num3)
		{
			return false;
		}
		uint num8 = BitConverter.ToUInt32(packet, num);
		num += 4;
		uint num9 = BitConverter.ToUInt32(packet, num);
		num += 4;
		num += 4;
		ulong num10 = BitConverter.ToUInt64(packet, num);
		num += 8;
		if (num8 == 0 || num8 > 1000000000)
		{
			return false;
		}
		if (num9 != 0 && num9 != uint.MaxValue && num9 > 86400000)
		{
			return false;
		}
		if (num10 < 1577836800000L || num10 > 4102444800000L)
		{
			return false;
		}
		int num11 = 0;
		int num12 = 0;
		var (num13, num14) = ReadVarInt(packet, num);
		if (num14 > 0 && num13 > 0 && num + num14 + 1 + 4 <= num3)
		{
			num11 = num13;
			num12 = (int)BitConverter.ToUInt32(packet, num + num14 + 1);
		}
		_logger.LogTrace("✨ ABN {Kind} target={Target} slot={Slot} abn={Abn} dur={Dur}ms caster={Caster} srcSkill={Skill}", flag ? "refresh" : "apply", num4, num6, num8, num9, num11, num12);
		AbnormalEventParsed?.Invoke(this, new AbnormalEvent
		{
			Timestamp = NowMs(),
			TargetId = num4,
			Slot = num6,
			AbnormalId = (int)num8,
			DurationMs = num9,
			ServerStampMs = num10,
			CasterId = num11,
			SourceSkillId = num12,
			IsRefresh = flag
		});
		return true;
	}

	private bool TryParseBossEnrageApply(byte[] packet, int offset, int recordEnd, int targetId)
	{
		int item = ReadVarInt(packet, offset).Length;
		if (item <= 0)
		{
			return false;
		}
		offset += item;
		if (offset + 4 + 4 + 4 + 8 > recordEnd)
		{
			return false;
		}
		uint num = BitConverter.ToUInt32(packet, offset);
		if (num != 10000008)
		{
			return false;
		}
		uint num2 = BitConverter.ToUInt32(packet, offset + 4);
		ulong num3 = BitConverter.ToUInt64(packet, offset + 12);
		if (num2 != uint.MaxValue)
		{
			return false;
		}
		if (num3 < 1577836800000L || num3 > 4102444800000L)
		{
			return false;
		}
		var (num4, num5) = ReadVarInt(packet, offset + 20);
		_logger.LogInformation("\ud83d\ude21 BOSS ENRAGE: entity {Entity} — abnormal {Abn} (Frenzy) applied, caster {Caster}", targetId, num, (num5 > 0) ? num4 : 0);
		BossEnrageDetected?.Invoke(this, new BossEnrageEvent
		{
			Timestamp = NowMs(),
			EntityId = targetId,
			AbnormalId = (int)num
		});
		return true;
	}

	private bool TryParseGroggyGuardPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 5 || packet[num + 1] != 224)
		{
			return false;
		}
		num += 2;
		if (num >= packet.Length)
		{
			return false;
		}
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0 || num2 <= 0)
		{
			return false;
		}
		num += num3;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 3 || packet[num + 1] != 1)
		{
			return false;
		}
		num += 2;
		if (num + 7 >= packet.Length)
		{
			return false;
		}
		uint num4 = (uint)(packet[num] | (packet[num + 1] << 8) | (packet[num + 2] << 16) | (packet[num + 3] << 24));
		uint num5 = (uint)(packet[num + 4] | (packet[num + 5] << 8) | (packet[num + 6] << 16) | (packet[num + 7] << 24));
		if (num4 == 0 || num4 > 1000000 || num5 > num4)
		{
			return false;
		}
		GroggyGuardEventParsed?.Invoke(this, new GroggyGuardEvent
		{
			Timestamp = NowMs(),
			ActorId = num2,
			MaxGuard = num4,
			CurrentGuard = num5
		});
		return true;
	}

	private bool TryParseSkillCastPacket(byte[] packet)
	{
		int num = 0;
		int item = ReadVarInt(packet, num).Length;
		if (item < 0)
		{
			return false;
		}
		num += item;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 9 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0 || num2 <= 0)
		{
			return false;
		}
		num += num3;
		num++;
		if (num + 3 >= packet.Length)
		{
			return false;
		}
		int num4 = packet[num] | (packet[num + 1] << 8) | (packet[num + 2] << 16) | (packet[num + 3] << 24);
		if (num4 <= 0 || num4 >= 10000000)
		{
			return false;
		}
		BossSkillCastEventParsed?.Invoke(this, new BossSkillCastEvent
		{
			Timestamp = NowMs(),
			CasterId = num2,
			SkillCode = num4
		});
		return true;
	}

	private bool TryParseAbnormalRemovePacket(byte[] packet)
	{
		int num = 0;
		var (num2, num3) = ReadVarInt(packet, num);
		if (num3 < 0)
		{
			return false;
		}
		num += num3;
		if (num + 1 >= packet.Length)
		{
			return false;
		}
		if (packet[num] != 44 || packet[num + 1] != 56)
		{
			return false;
		}
		num += 2;
		int num4 = num3 + num2 - 4;
		int num5 = AbnormalRecordEnd(packet, num2, num3);
		var (num6, num7) = ReadVarInt(packet, num);
		if (num7 < 0 || num6 <= 0)
		{
			return false;
		}
		num += num7;
		if (num >= num5)
		{
			return false;
		}
		byte b = packet[num++];
		if (b == 0 || num + b * 3 > num4)
		{
			return false;
		}
		Span<int> span = stackalloc int[(int)b];
		for (int i = 0; i < b; i++)
		{
			if (num >= num5)
			{
				return false;
			}
			byte b2 = packet[num];
			if (b2 != 0 && b2 != 7)
			{
				return false;
			}
			var (num8, num9) = ReadVarInt(packet, num + 1);
			if (num9 <= 0 || num8 < 0)
			{
				return false;
			}
			int num10 = num + 1 + num9 + 1;
			if (b2 == 7)
			{
				if (num10 > num5)
				{
					return false;
				}
				int item = ReadVarInt(packet, num10).Length;
				if (item <= 0)
				{
					return false;
				}
				num10 += item + 4 + 4;
			}
			if (num10 > num5)
			{
				return false;
			}
			span[i] = num8;
			num = num10;
		}
		if (num != num4)
		{
			return false;
		}
		long timestamp = NowMs();
		for (int j = 0; j < b; j++)
		{
			_logger.LogTrace("✨ ABN remove target={Target} slot={Slot}", num6, span[j]);
			AbnormalEventParsed?.Invoke(this, new AbnormalEvent
			{
				Timestamp = timestamp,
				TargetId = num6,
				Slot = span[j],
				IsRemove = true
			});
		}
		return true;
	}

	private static int FindArrayIndex(byte[] data, byte[] pattern)
	{
		if (pattern.Length == 0)
		{
			return 0;
		}
		if (data.Length < pattern.Length)
		{
			return -1;
		}
		int[] array = new int[pattern.Length];
		int num = 0;
		for (int i = 1; i < pattern.Length; i++)
		{
			while (num > 0 && pattern[i] != pattern[num])
			{
				num = array[num - 1];
			}
			if (pattern[i] == pattern[num])
			{
				num++;
			}
			array[i] = num;
		}
		int num2 = 0;
		int num3 = 0;
		while (num2 < data.Length)
		{
			if (data[num2] == pattern[num3])
			{
				num2++;
				num3++;
				if (num3 == pattern.Length)
				{
					return num2 - num3;
				}
			}
			else if (num3 > 0)
			{
				num3 = array[num3 - 1];
			}
			else
			{
				num2++;
			}
		}
		return -1;
	}

	public static (int outLen, int consumedIn) LenientLz4Decode_Public(byte[] comp, byte[] target, int maxOutput)
	{
		return LenientLz4Decode(comp, target, maxOutput);
	}

	private static (int outLen, int consumedIn) LenientLz4Decode(byte[] comp, byte[] target, int maxOutput)
	{
		int num = 0;
		int num2 = 0;
		while (num < comp.Length)
		{
			if (num2 >= maxOutput)
			{
				return (outLen: num2, consumedIn: num);
			}
			int num3 = comp[num++] & 0xFF;
			int num4 = num3 >> 4;
			int num5 = num3 & 0xF;
			if (num4 == 15)
			{
				while (num < comp.Length)
				{
					int num6 = comp[num++] & 0xFF;
					num4 += num6;
					if (num6 != 255)
					{
						break;
					}
				}
			}
			if (num + num4 > comp.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			if (num2 + num4 > target.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			Buffer.BlockCopy(comp, num, target, num2, num4);
			num2 += num4;
			num += num4;
			if (num >= comp.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			if (num + 2 > comp.Length)
			{
				return (outLen: num2, consumedIn: num);
			}
			int num7 = (comp[num] & 0xFF) | ((comp[num + 1] & 0xFF) << 8);
			num += 2;
			if (num7 == 0 || num7 > num2)
			{
				return (outLen: num2, consumedIn: num);
			}
			if (num5 == 15)
			{
				while (num < comp.Length)
				{
					int num8 = comp[num++] & 0xFF;
					num5 += num8;
					if (num8 != 255)
					{
						break;
					}
				}
			}
			num5 += 4;
			int num9 = num2 - num7;
			for (int i = 0; i < num5; i++)
			{
				if (num2 >= target.Length)
				{
					return (outLen: num2, consumedIn: num);
				}
				target[num2++] = target[num9 + i];
			}
		}
		return (outLen: num2, consumedIn: num);
	}

	private void ScanDecompressedBundleBrutal(byte[] data, int length, List<(int Start, int End)>? compressedExtents = null)
	{
		if (length < 12)
		{
			return;
		}
		int num = Math.Min(length, data.Length);
		for (int i = 0; i < num - 12; i++)
		{
			if (data[i] == 4 && data[i + 1] == 56 && !InExtent(compressedExtents, i))
			{
				int num2 = Math.Min(60, num - i);
				byte[] array = new byte[1 + num2];
				array[0] = (byte)num2;
				Buffer.BlockCopy(data, i, array, 1, num2);
				TryParseDamagePacket(array);
			}
		}
	}

	private List<(int Start, int End)>? FindDecodableBundleExtents(byte[] data)
	{
		List<(int, int)> list = null;
		for (int i = 1; i + 6 <= data.Length; i++)
		{
			if (data[i] != byte.MaxValue || data[i + 1] != byte.MaxValue)
			{
				continue;
			}
			int num = data[i + 2] | (data[i + 3] << 8) | (data[i + 4] << 16) | (data[i + 5] << 24);
			if (num <= 0 || num > 1000000)
			{
				continue;
			}
			int num2 = i + 6;
			for (int j = 1; j <= 3 && i - j >= 0; j++)
			{
				(int Value, int Length) tuple = ReadVarInt(data, i - j);
				var (num3, _) = tuple;
				if (tuple.Length == j)
				{
					int num4 = num2 + num3 - 10;
					if (num4 > num2 && num4 <= data.Length && Lz4BlockDecodesExactly(data, num2, num4, num))
					{
						(list ?? (list = new List<(int, int)>())).Add((num2, num4));
						i = num4 - 1;
						break;
					}
				}
			}
		}
		return list;
	}

	private static bool Lz4BlockDecodesExactly(byte[] src, int start, int end, int dsz)
	{
		long num = 0L;
		int num2 = start;
		while (num2 < end)
		{
			byte b = src[num2++];
			long num3 = b >> 4;
			if (num3 == 15)
			{
				int num4;
				do
				{
					if (num2 >= end)
					{
						return false;
					}
					num4 = src[num2++];
					num3 += num4;
				}
				while (num4 == 255);
			}
			if (num2 + num3 > end)
			{
				return false;
			}
			num2 += (int)num3;
			num += num3;
			if (num > dsz)
			{
				return false;
			}
			if (num2 == end)
			{
				return num == dsz;
			}
			if (num2 + 2 > end)
			{
				return false;
			}
			int num5 = src[num2] | (src[num2 + 1] << 8);
			num2 += 2;
			if (num5 == 0 || num5 > num)
			{
				return false;
			}
			long num6 = b & 0xF;
			if (num6 == 15)
			{
				int num7;
				do
				{
					if (num2 >= end)
					{
						return false;
					}
					num7 = src[num2++];
					num6 += num7;
				}
				while (num7 == 255);
			}
			num += num6 + 4;
			if (num > dsz)
			{
				return false;
			}
		}
		return false;
	}

	private static bool InExtent(List<(int Start, int End)>? extents, int position)
	{
		if (extents == null)
		{
			return false;
		}
		foreach (var (num, num2) in extents)
		{
			if (position >= num && position < num2)
			{
				return true;
			}
		}
		return false;
	}

	private void ScanForUnframedBundles(byte[] payload)
	{
		if (payload.Length < 12)
		{
			return;
		}
		int num = 64;
		for (int i = 0; i < payload.Length - 6; i++)
		{
			if (payload[i] != byte.MaxValue || payload[i + 1] != byte.MaxValue)
			{
				continue;
			}
			int num2 = payload[i + 2] | (payload[i + 3] << 8) | (payload[i + 4] << 16) | (payload[i + 5] << 24);
			if (num2 <= 0 || num2 > 1000000)
			{
				continue;
			}
			int num3 = i + 6;
			if (num3 >= payload.Length)
			{
				continue;
			}
			if (num-- <= 0)
			{
				break;
			}
			int num4 = payload.Length - num3;
			byte[] array = new byte[num2 + 64];
			int num5;
			try
			{
				num5 = LZ4Codec.Decode(payload, num3, num4, array, 0, array.Length);
			}
			catch
			{
				continue;
			}
			if (num5 < num2)
			{
				byte[] array2 = new byte[num4];
				Buffer.BlockCopy(payload, num3, array2, 0, num4);
				int item = LenientLz4Decode(array2, array, array.Length).outLen;
				if (item < num2)
				{
					continue;
				}
				num5 = item;
			}
			ScanDecompressedBundleBrutal(array, num5);
		}
	}
}
