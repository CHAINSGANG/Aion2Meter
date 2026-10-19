using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Schedules;
using AionFlex.Core.Servers;
using AionFlex.Core.Validation;
using Microsoft.Extensions.Logging;

namespace AionFlex.Application.Data;

public sealed class DataRepository : IDataRepository
{
	private readonly ILogger<DataRepository> _logger;

	private readonly ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>> _byActorStorage = new ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>>();

	private readonly ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>> _byTargetStorage = new ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>>();

	private readonly ConcurrentDictionary<int, string> _nicknameStorage = new ConcurrentDictionary<int, string>();

	private readonly ConcurrentDictionary<int, bool> _confirmedPlayers = new ConcurrentDictionary<int, bool>();

	private readonly ConcurrentDictionary<int, string> _mobNameStorage = new ConcurrentDictionary<int, string>();

	private readonly ConcurrentDictionary<int, int> _zKeyStorage = new ConcurrentDictionary<int, int>();

	private readonly ConcurrentDictionary<int, int> _entityNpcIdStorage = new ConcurrentDictionary<int, int>();

	private readonly ConcurrentDictionary<int, (int GS, int CP, int Level)> _playerStats = new ConcurrentDictionary<int, (int, int, int)>();

	private readonly ConcurrentDictionary<int, CombatPowerObservation> _cpObservations = new ConcurrentDictionary<int, CombatPowerObservation>();

	private readonly ConcurrentDictionary<int, JobClass> _jobClasses = new ConcurrentDictionary<int, JobClass>();

	private readonly ConcurrentDictionary<int, string> _legions = new ConcurrentDictionary<int, string>();

	private readonly ConcurrentDictionary<int, int> _summonStorage = new ConcurrentDictionary<int, int>();

	private readonly ConcurrentDictionary<int, string> _entityFingerprints = new ConcurrentDictionary<int, string>();

	private readonly ConcurrentDictionary<string, int> _fingerprintOwners = new ConcurrentDictionary<string, int>();

	private string? _localPlayerName;

	private int? _localPlayerId;

	private int? _lastBoundLocalPlayerId;

	private string? _currentServerHost;

	private int? _currentServerId;

	private IReadOnlyList<string?> _installRegions = Array.Empty<string>();

	private string? _clientRegion;

	private readonly object _localPlayerLock = new object();

	private int _currentTarget;

	private readonly object _targetLock = new object();

	private long _eventVersion;

	public const long DefaultRetentionWindowMs = 1800000L;

	public const int MaxEventsPerQueue = 150000;

	private readonly ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>> _healEventsByHealer = new ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>>();

	private long _healEventVersion;

	private long _diagEvictedEvents;

	private readonly ConcurrentDictionary<int, (string Name, long EndMs)[]> _endedNameHolders = new ConcurrentDictionary<int, (string, long)[]>();

	private readonly ConcurrentDictionary<int, bool> _storedNameConfirmed = new ConcurrentDictionary<int, bool>();

	private long _newestEventTimestampMs;

	private long _identitySeq;

	private long _localBindSeq;

	private readonly ConcurrentDictionary<int, long> _playerConfirmedSeq = new ConcurrentDictionary<int, long>();

	private readonly ConcurrentDictionary<int, (int OwnerId, long EndMs)[]> _endedSummonLives = new ConcurrentDictionary<int, (int, long)[]>();

	private const int NoSummonOwner = 0;

	private readonly ServerClockAttribution _serverClock = new ServerClockAttribution();

	private readonly object _serverClockLock = new object();

	private readonly ConcurrentDictionary<int, int> _entityLifeGeneration = new ConcurrentDictionary<int, int>();

	private long _entityLifeRebaseVersion;

	private readonly ConcurrentDictionary<int, long> _entityAppearanceStartMs = new ConcurrentDictionary<int, long>();

	public long RetentionWindowMs { get; set; } = 1800000L;

	public long EvictedEventCount => Interlocked.Read(in _diagEvictedEvents);

	public event EventHandler<string?>? ClientRegionChanged;

	public DataRepository(ILogger<DataRepository> logger)
	{
		_logger = logger;
	}

	public void AppendDamageEvent(DamageEvent damageEvent)
	{
		ConcurrentQueue<DamageEvent> orAdd = _byActorStorage.GetOrAdd(damageEvent.ActorId, (int _) => new ConcurrentQueue<DamageEvent>());
		orAdd.Enqueue(damageEvent);
		ConcurrentQueue<DamageEvent> orAdd2 = _byTargetStorage.GetOrAdd(damageEvent.TargetId, (int _) => new ConcurrentQueue<DamageEvent>());
		orAdd2.Enqueue(damageEvent);
		TrimQueue(orAdd, damageEvent.Timestamp);
		TrimQueue(orAdd2, damageEvent.Timestamp);
		long num;
		while (damageEvent.Timestamp > (num = Interlocked.Read(in _newestEventTimestampMs)) && Interlocked.CompareExchange(ref _newestEventTimestampMs, damageEvent.Timestamp, num) != num)
		{
		}
		Interlocked.Increment(ref _eventVersion);
		_logger.LogTrace("Damage event appended: Actor={ActorId}, Target={TargetId}, Damage={Damage}", damageEvent.ActorId, damageEvent.TargetId, damageEvent.Damage);
	}

	public void AppendHealEvent(DamageEvent healEvent)
	{
		ConcurrentQueue<DamageEvent> orAdd = _healEventsByHealer.GetOrAdd(healEvent.ActorId, (int _) => new ConcurrentQueue<DamageEvent>());
		orAdd.Enqueue(healEvent);
		TrimQueue(orAdd, healEvent.Timestamp);
		Interlocked.Increment(ref _healEventVersion);
	}

	public IReadOnlyList<DamageEvent> GetHealEvents()
	{
		return (from e in _healEventsByHealer.Values.SelectMany((ConcurrentQueue<DamageEvent> q) => q)
			orderby e.Timestamp
			select e).ToArray();
	}

	public long GetHealEventVersion()
	{
		return Interlocked.Read(in _healEventVersion);
	}

	private void TrimQueue(ConcurrentQueue<DamageEvent> queue, long newestTimestamp)
	{
		long retentionWindowMs = RetentionWindowMs;
		DamageEvent result2;
		if (retentionWindowMs < long.MaxValue)
		{
			long num = newestTimestamp - retentionWindowMs;
			DamageEvent result;
			while (queue.TryPeek(out result) && result.Timestamp < num && queue.TryDequeue(out result2))
			{
				Interlocked.Increment(ref _diagEvictedEvents);
			}
		}
		while (queue.Count > 150000 && queue.TryDequeue(out result2))
		{
			Interlocked.Increment(ref _diagEvictedEvents);
		}
	}

	public IReadOnlyCollection<DamageEvent> GetDamageEventsByActor(int actorId)
	{
		if (!_byActorStorage.TryGetValue(actorId, out ConcurrentQueue<DamageEvent> value))
		{
			return Array.Empty<DamageEvent>();
		}
		return value.ToList();
	}

	public IReadOnlyCollection<DamageEvent> GetDamageEventsByTarget(int targetId)
	{
		if (!_byTargetStorage.TryGetValue(targetId, out ConcurrentQueue<DamageEvent> value))
		{
			return Array.Empty<DamageEvent>();
		}
		return value.ToList();
	}

	public int GetDamageEventCountByTarget(int targetId)
	{
		if (!_byTargetStorage.TryGetValue(targetId, out ConcurrentQueue<DamageEvent> value))
		{
			return 0;
		}
		return value.Count;
	}

	public IReadOnlyCollection<int> GetAllActorIds()
	{
		return _byActorStorage.Keys.ToList();
	}

	public IReadOnlyCollection<int> GetAllTargetIds()
	{
		return _byTargetStorage.Keys.ToList();
	}

	public void SetNickname(int playerId, string nickname, bool isConfirmedPlayer = false, bool isAuthoritativeSource = false)
	{
		if (!PlayerNameValidator.IsPlausible(nickname))
		{
			_logger.LogDebug("Nickname registration REJECTED (implausible — likely captured JSON/telemetry fragment or misparse): Player {PlayerId}: '{Nickname}'", playerId, nickname);
			return;
		}
		if (isConfirmedPlayer)
		{
			_confirmedPlayers.TryAdd(playerId, value: true);
		}
		if (isAuthoritativeSource)
		{
			TryUpdateLocalPlayerId(playerId, nickname);
		}
		if (isConfirmedPlayer)
		{
			_playerConfirmedSeq[playerId] = Interlocked.Increment(ref _identitySeq);
			EndSummonLifeOfConfirmedPlayer(playerId, nickname);
		}
		if (_nicknameStorage.TryGetValue(playerId, out string existingNickname) && existingNickname == nickname)
		{
			if (isConfirmedPlayer)
			{
				_storedNameConfirmed[playerId] = true;
			}
			return;
		}
		if (string.IsNullOrEmpty(nickname) || (nickname.Length == 1 && !char.IsLetter(nickname[0])))
		{
			_logger.LogDebug("Nickname registration SKIPPED (single-char garbage): Player {PlayerId}: '{ExistingNickname}' -x> '{NewNickname}'", playerId, existingNickname, nickname);
			return;
		}
		bool value = default;
		if ((existingNickname != null && _storedNameConfirmed.TryGetValue(playerId, out value)) & value)
		{
			long num = Interlocked.Read(in _newestEventTimestampMs);
			if (num > 0)
			{
				long endMs = num + 1;
				_endedNameHolders.AddOrUpdate(playerId, (int _) => new (string, long)[1] { (existingNickname, endMs) }, (int _, (string Name, long EndMs)[] holders) =>
				{
					int num2 = 0;
					(string, long)[] array = new (string, long)[1 + holders.Length];
					ReadOnlySpan<(string, long)> readOnlySpan = new ReadOnlySpan<(string, long)>(holders);
					readOnlySpan.CopyTo(new Span<(string, long)>(array).Slice(num2, readOnlySpan.Length));
					num2 += readOnlySpan.Length;
					array[num2] = (existingNickname, endMs);
					num2++;
					return array;
				});
				_logger.LogInformation("♻\ufe0f Player id {PlayerId} changed hands: '{OldNickname}' keeps its events before {EndMs}", playerId, existingNickname, endMs);
			}
		}
		_nicknameStorage[playerId] = nickname;
		_storedNameConfirmed[playerId] = isConfirmedPlayer;
		_logger.LogInformation("NICKNAME STORED: Player {PlayerId}: '{OldNickname}' -> '{NewNickname}'", playerId, existingNickname ?? "(none)", nickname);
	}

	public string? GetNicknameAt(int playerId, long timestampMs)
	{
		if (_endedNameHolders.TryGetValue(playerId, out (string, long)[] value))
		{
			(string, long)[] array = value;
			for (int i = 0; i < array.Length; i++)
			{
				var (result, num) = array[i];
				if (timestampMs < num)
				{
					return result;
				}
			}
		}
		return GetNickname(playerId);
	}

	private void TryUpdateLocalPlayerId(int playerId, string nickname)
	{
		lock (_localPlayerLock)
		{
			if (_localPlayerName != null && !(nickname != _localPlayerName))
			{
				_lastBoundLocalPlayerId = playerId;
				int? localPlayerId = _localPlayerId;
				if (localPlayerId != playerId)
				{
					_localPlayerId = playerId;
					Volatile.Write(ref _localBindSeq, Interlocked.Increment(ref _identitySeq));
					_logger.LogInformation("LOCAL PLAYER ID SET: {OldId} -> {NewId} (nickname '{Nickname}')", localPlayerId?.ToString() ?? "null", playerId, nickname);
				}
			}
		}
	}

	public string? GetNickname(int playerId)
	{
		if (!_nicknameStorage.TryGetValue(playerId, out string value))
		{
			return null;
		}
		return value;
	}

	public IReadOnlyDictionary<int, string> GetAllNicknames()
	{
		return _nicknameStorage;
	}

	public bool IsConfirmedPlayer(int entityId)
	{
		return _confirmedPlayers.ContainsKey(entityId);
	}

	public bool IsPresentConfirmedPlayer(int entityId)
	{
		if (_playerConfirmedSeq.TryGetValue(entityId, out var value))
		{
			return value > Volatile.Read(in _localBindSeq);
		}
		return false;
	}

	private void EndSummonLifeOfConfirmedPlayer(int playerId, string nickname)
	{
		if (_summonStorage.TryGetValue(playerId, out var value))
		{
			long endMs = Interlocked.Read(in _newestEventTimestampMs) + 1;
			_logger.LogInformation("♻\ufe0f Summon owner {OwnerId} of {PlayerId} dropped: the id is now player '{Nickname}'", value, playerId, nickname);
			EndSummonLife(playerId, endMs, value);
		}
	}

	public void RegisterSummon(int summonId, int summonerId)
	{
		_summonStorage[summonId] = summonerId;
		_logger.LogTrace("Summon registered: {SummonId} -> {SummonerId}", summonId, summonerId);
		if (_entityFingerprints.TryGetValue(summonId, out string value) && !_fingerprintOwners.ContainsKey(value))
		{
			_fingerprintOwners[value] = summonerId;
			_logger.LogInformation("\ud83d\udd11 FINGERPRINT LEARNED: '{Fp}' → owner {OwnerId} (from summon {SummonId})", value, summonerId, summonId);
		}
	}

	public int? GetSummoner(int summonId)
	{
		if (_summonStorage.TryGetValue(summonId, out var value))
		{
			return value;
		}
		if (_entityFingerprints.TryGetValue(summonId, out string value2) && _fingerprintOwners.TryGetValue(value2, out var value3))
		{
			return value3;
		}
		return null;
	}

	public void EndSummonLife(int summonId, long endMs, int? lifeOwnerId = null)
	{
		bool flag = _summonStorage.TryGetValue(summonId, out var value);
		int ownerId = lifeOwnerId ?? (flag ? value : 0);
		if (ownerId != 0 || _endedSummonLives.ContainsKey(summonId))
		{
			_endedSummonLives.AddOrUpdate(summonId, (int _) => new (int, long)[1] { (ownerId, endMs) }, (int _, (int OwnerId, long EndMs)[] lives) =>
			{
				int num = 0;
				(int, long)[] array = new (int, long)[1 + lives.Length];
				ReadOnlySpan<(int, long)> readOnlySpan = new ReadOnlySpan<(int, long)>(lives);
				readOnlySpan.CopyTo(new Span<(int, long)>(array).Slice(num, readOnlySpan.Length));
				num += readOnlySpan.Length;
				array[num] = (ownerId, endMs);
				num++;
				return array;
			});
			if (flag)
			{
				_summonStorage.TryRemove(new KeyValuePair<int, int>(summonId, value));
			}
			Interlocked.Increment(ref _eventVersion);
			_logger.LogInformation("♻\ufe0f Summon {SummonId} life ended at {EndMs}: owner {OwnerId} kept for its earlier hits", summonId, endMs, (ownerId == 0) ? "(none)" : ((object)ownerId));
		}
	}

	public int? GetSummonerAt(int summonId, long timestampMs)
	{
		if (_endedSummonLives.TryGetValue(summonId, out (int, long)[] value))
		{
			(int, long)[] array = value;
			for (int i = 0; i < array.Length; i++)
			{
				var (num, num2) = array[i];
				if (timestampMs < num2)
				{
					if (num != 0)
					{
						return num;
					}
					return null;
				}
			}
		}
		return GetSummoner(summonId);
	}

	public int GetSummonLifeGeneration(int summonId)
	{
		if (!_endedSummonLives.TryGetValue(summonId, out (int, long)[] value))
		{
			return 0;
		}
		return value.Length;
	}

	public void SetEntityFingerprint(int entityId, string fingerprint)
	{
		bool flag = !_entityFingerprints.ContainsKey(entityId);
		_entityFingerprints[entityId] = fingerprint;
		if (flag)
		{
			_logger.LogDebug("\ud83d\udd11 FINGERPRINT STORED: Entity {EntityId} → '{Fp}'", entityId, fingerprint);
		}
	}

	public void SetLocalPlayerName(string characterName)
	{
		lock (_localPlayerLock)
		{
			if (!(_localPlayerName == characterName))
			{
				string localPlayerName = _localPlayerName;
				_localPlayerName = characterName;
				_localPlayerId = null;
				_lastBoundLocalPlayerId = null;
				_logger.LogInformation("LOCAL PLAYER NAME SET: '{OldName}' -> '{NewName}'", localPlayerName ?? "(none)", characterName);
				if (localPlayerName != null)
				{
					_logger.LogWarning("CHARACTER SWITCH DETECTED: Flushing DPS data for new character '{NewName}'", characterName);
					FlushDamageData();
				}
			}
		}
	}

	public string? GetLocalPlayerName()
	{
		lock (_localPlayerLock)
		{
			return _localPlayerName;
		}
	}

	public void SetCurrentServerHost(string? host)
	{
		lock (_localPlayerLock)
		{
			if (!(_currentServerHost == host))
			{
				_currentServerHost = host;
				_logger.LogDebug("\ud83d\udccd Repository server host updated: {Host}", host ?? "(null)");
			}
		}
	}

	public string? GetCurrentServerHost()
	{
		lock (_localPlayerLock)
		{
			return _currentServerHost;
		}
	}

	public void RecordServerClockReading(TimeSpan offset, int? namedServerId, long nowMs)
	{
		GameRegion? gameRegion;
		if (namedServerId.HasValue)
		{
			int valueOrDefault = namedServerId.GetValueOrDefault();
			gameRegion = RegionOfServer(valueOrDefault);
		}
		else
		{
			gameRegion = null;
		}
		GameRegion? gameRegion2 = gameRegion;
		lock (_serverClockLock)
		{
			_serverClock.OnReading(offset, gameRegion2, nowMs);
		}
		_logger.LogDebug("\ud83d\udd52 Server clock read from the game: {Offset} (server {ServerId} → {Region})", GameServerClock.OffsetLabel(offset), namedServerId?.ToString() ?? "not named", gameRegion2?.ToString() ?? "pending");
	}

	public void ConfirmServerClockServer(int serverId, long nowMs)
	{
		GameRegion? gameRegion = RegionOfServer(serverId);
		if (gameRegion.HasValue)
		{
			GameRegion valueOrDefault = gameRegion.GetValueOrDefault();
			lock (_serverClockLock)
			{
				_serverClock.OnServerConfirmed(valueOrDefault, nowMs);
			}
		}
	}

	public (GameRegion Region, TimeSpan Offset, long SinceMs)? GetServerClock()
	{
		lock (_serverClockLock)
		{
			(GameRegion, TimeSpan)? live = _serverClock.Live;
			(GameRegion, TimeSpan, long)? result;
			if (live.HasValue)
			{
				(GameRegion, TimeSpan) valueOrDefault = live.GetValueOrDefault();
				result = (valueOrDefault.Item1, valueOrDefault.Item2, _serverClock.LiveSinceMs);
			}
			else
			{
				result = null;
			}
			return result;
		}
	}

	public bool TryTakeStableServerClock(long nowMs, out GameRegion region, out TimeSpan offset)
	{
		lock (_serverClockLock)
		{
			bool result = _serverClock.TryTakeStable(nowMs, out (GameRegion, TimeSpan) pair);
			(region, offset) = pair;
			return result;
		}
	}

	private GameRegion? RegionOfServer(int serverId)
	{
		IReadOnlyList<string> installRegions;
		lock (_localPlayerLock)
		{
			installRegions = _installRegions;
		}
		return GameRegionExtensions.TryFromClient(ClientRegionPolicy.Resolve(installRegions.ToArray(), serverId), serverId);
	}

	public void SetClientRegion(string? regionCode)
	{
		SetClientInstallRegions((regionCode == null) ? Array.Empty<string>() : new string[1] { regionCode });
	}

	public void SetClientInstallRegions(IReadOnlyList<string?> regionCodes)
	{
		string region;
		lock (_localPlayerLock)
		{
			_installRegions = regionCodes.ToArray();
			if (!TryRecomputeClientRegionLocked(out region))
			{
				return;
			}
		}
		ClientRegionChanged?.Invoke(this, region);
	}

	private bool TryRecomputeClientRegionLocked(out string? region)
	{
		region = ClientRegionPolicy.Resolve(_installRegions, _currentServerId);
		if (_clientRegion == region)
		{
			return false;
		}
		_clientRegion = region;
		_logger.LogInformation("\ud83c\udf10 Client region detected: {Region} (installs [{Installs}], server {ServerId})", region ?? "(unknown)", string.Join(", ", _installRegions.Select((string r) => r ?? "?")), _currentServerId?.ToString() ?? "(none)");
		return true;
	}

	public string? GetClientRegion()
	{
		lock (_localPlayerLock)
		{
			return _clientRegion;
		}
	}

	public void SetCurrentServerId(int? serverId)
	{
		string region;
		lock (_localPlayerLock)
		{
			if (_currentServerId == serverId)
			{
				return;
			}
			_currentServerId = serverId;
			_logger.LogInformation("\ud83d\udccd Repository server ID updated: {Id}", serverId?.ToString() ?? "(null)");
			if (!TryRecomputeClientRegionLocked(out region))
			{
				return;
			}
		}
		ClientRegionChanged?.Invoke(this, region);
	}

	public int? GetCurrentServerId()
	{
		lock (_localPlayerLock)
		{
			return _currentServerId;
		}
	}

	public int? GetLocalPlayerEntityId()
	{
		lock (_localPlayerLock)
		{
			return _localPlayerId;
		}
	}

	public int? GetLastBoundLocalPlayerEntityId()
	{
		lock (_localPlayerLock)
		{
			return _lastBoundLocalPlayerId;
		}
	}

	public void ResetLocalPlayerEntityId()
	{
		lock (_localPlayerLock)
		{
			if (_localPlayerId.HasValue)
			{
				_logger.LogInformation("\ud83c\udfae LOCAL PLAYER ID RESET: {OldId} → null (instance entry, keeping name '{Name}')", _localPlayerId, _localPlayerName);
				_localPlayerId = null;
			}
		}
	}

	public void ClearLocalPlayer()
	{
		lock (_localPlayerLock)
		{
			string localPlayerName = _localPlayerName;
			_localPlayerName = null;
			_localPlayerId = null;
			_lastBoundLocalPlayerId = null;
			if (localPlayerName != null)
			{
				_logger.LogInformation("\ud83c\udfae LOCAL PLAYER CLEARED: '{OldName}' → (none)", localPlayerName);
			}
		}
	}

	public string? GetNicknameWithLocalPlayerFallback(int playerId)
	{
		if (_nicknameStorage.TryGetValue(playerId, out string value))
		{
			return value;
		}
		lock (_localPlayerLock)
		{
			if (_localPlayerId == playerId && _localPlayerName != null)
			{
				return _localPlayerName;
			}
		}
		return null;
	}

	public void SetMobName(int targetId, string name)
	{
		_mobNameStorage[targetId] = name;
		_logger.LogInformation("\ud83c\udfaf MOB NAME STORED: Target {TargetId} = '{Name}'", targetId, name);
	}

	public bool RemoveMobName(int targetId)
	{
		if (!_mobNameStorage.TryRemove(targetId, out string value))
		{
			return false;
		}
		_logger.LogInformation("\ud83c\udfaf MOB NAME REVOKED: Target {TargetId} was '{Name}'", targetId, value);
		return true;
	}

	public string? GetMobName(int targetId)
	{
		if (!_mobNameStorage.TryGetValue(targetId, out string value))
		{
			return null;
		}
		return value;
	}

	public void SetEntityZKey(int entityId, int zKey)
	{
		_zKeyStorage[entityId] = zKey;
	}

	public int GetEntityZKey(int entityId)
	{
		if (!_zKeyStorage.TryGetValue(entityId, out var value))
		{
			return 0;
		}
		return value;
	}

	public void SetEntityNpcId(int entityId, int npcId)
	{
		_entityNpcIdStorage[entityId] = npcId;
	}

	public int GetEntityNpcId(int entityId)
	{
		if (!_entityNpcIdStorage.TryGetValue(entityId, out var value))
		{
			return 0;
		}
		return value;
	}

	public void SetPlayerStats(int entityId, int gearScore, int combatPower, int level)
	{
		_playerStats[entityId] = (gearScore, combatPower, level);
		_logger.LogInformation("Player stats: Entity={Entity}, GS={GS}, CP={CP}, Level={Level}", entityId, gearScore, combatPower, level);
	}

	public (int GearScore, int CombatPower, int Level) GetPlayerStats(int entityId)
	{
		if (!_playerStats.TryGetValue(entityId, out (int, int, int) value))
		{
			return (GearScore: 0, CombatPower: 0, Level: 0);
		}
		return value;
	}

	public void MarkCombatPowerObserved(int entityId, long observedAtMs, CombatPowerSource source, string? name = null)
	{
		_cpObservations[entityId] = new CombatPowerObservation(observedAtMs, source, name);
	}

	public CombatPowerObservation? GetCombatPowerObservation(int entityId)
	{
		if (!_cpObservations.TryGetValue(entityId, out var value))
		{
			return null;
		}
		return value;
	}

	public void SetJobClass(int entityId, JobClass jobClass)
	{
		if (jobClass != JobClass.Unknown)
		{
			_jobClasses[entityId] = jobClass;
		}
	}

	public JobClass GetJobClass(int entityId)
	{
		if (!_jobClasses.TryGetValue(entityId, out var value))
		{
			return JobClass.Unknown;
		}
		return value;
	}

	public void SetLegion(int entityId, string legion)
	{
		if (!string.IsNullOrEmpty(legion))
		{
			_legions[entityId] = legion;
		}
	}

	public string? GetLegion(int entityId)
	{
		if (!_legions.TryGetValue(entityId, out string value))
		{
			return null;
		}
		return value;
	}

	public void SetCurrentTarget(int targetId)
	{
		lock (_targetLock)
		{
			_currentTarget = targetId;
		}
		_logger.LogTrace("Current target set to: {TargetId}", targetId);
	}

	public int GetCurrentTarget()
	{
		lock (_targetLock)
		{
			return _currentTarget;
		}
	}

	public void FlushDamageData()
	{
		_byActorStorage.Clear();
		_byTargetStorage.Clear();
		_healEventsByHealer.Clear();
		Interlocked.Increment(ref _healEventVersion);
		_entityFingerprints.Clear();
		_fingerprintOwners.Clear();
		Interlocked.Exchange(ref _eventVersion, 0L);
		_logger.LogInformation("Damage data flushed (summons, mob names, z-keys preserved)");
	}

	public void FlushAllData()
	{
		FlushDamageData();
		_nicknameStorage.Clear();
		_confirmedPlayers.Clear();
		_playerConfirmedSeq.Clear();
		_mobNameStorage.Clear();
		_logger.LogInformation("All data flushed");
	}

	public int GetTotalEventCount()
	{
		return _byActorStorage.Values.Sum((ConcurrentQueue<DamageEvent> q) => q.Count);
	}

	public long GetEventVersion()
	{
		return Interlocked.Read(in _eventVersion);
	}

	public int PurgeEntityLife(int entityId, long cutoffMs)
	{
		int num = 0;
		if (_byTargetStorage.TryGetValue(entityId, out ConcurrentQueue<DamageEvent> value))
		{
			num = value.Count(EndedLife);
			if (num > 0)
			{
				ReplaceQueue(_byTargetStorage, entityId, value);
			}
		}
		foreach (var (key, concurrentQueue2) in _byActorStorage)
		{
			if (concurrentQueue2.Any(EndedLife))
			{
				ReplaceQueue(_byActorStorage, key, concurrentQueue2);
			}
		}
		_entityLifeGeneration.AddOrUpdate(entityId, 1, (int _, int g) => g + 1);
		Interlocked.Increment(ref _entityLifeRebaseVersion);
		Interlocked.Increment(ref _eventVersion);
		_logger.LogInformation("♻\ufe0f Entity {EntityId} life ended: dropped {Purged} events targeting it before {CutoffMs}", entityId, num, cutoffMs);
		return num;
		bool EndedLife(DamageEvent e)
		{
			if (e.TargetId == entityId)
			{
				return e.Timestamp < cutoffMs;
			}
			return false;
		}
		void ReplaceQueue(ConcurrentDictionary<int, ConcurrentQueue<DamageEvent>> storage, int key2, ConcurrentQueue<DamageEvent> queue)
		{
			ConcurrentQueue<DamageEvent> concurrentQueue3 = new ConcurrentQueue<DamageEvent>(queue.Where((DamageEvent e) => !EndedLife(e)));
			if (concurrentQueue3.IsEmpty)
			{
				storage.TryRemove(new KeyValuePair<int, ConcurrentQueue<DamageEvent>>(key2, queue));
			}
			else
			{
				storage.TryUpdate(key2, concurrentQueue3, queue);
			}
		}
	}

	public int GetEntityLifeGeneration(int entityId)
	{
		if (!_entityLifeGeneration.TryGetValue(entityId, out var value))
		{
			return 0;
		}
		return value;
	}

	public long GetEntityLifeRebaseVersion()
	{
		return Interlocked.Read(in _entityLifeRebaseVersion);
	}

	public void StartEntityAppearance(int entityId, long appearMs)
	{
		if (_entityAppearanceStartMs.AddOrUpdate(entityId, appearMs, (int _, long ms) => Math.Max(ms, appearMs)) == appearMs)
		{
			Interlocked.Increment(ref _eventVersion);
			_logger.LogDebug("♻\ufe0f Entity {EntityId} appeared again at {AppearMs}", entityId, appearMs);
		}
	}

	public long GetEntityAppearanceStartMs(int entityId)
	{
		if (!_entityAppearanceStartMs.TryGetValue(entityId, out var value))
		{
			return 0L;
		}
		return value;
	}

	public void LogCurrentState()
	{
		int totalEventCount = GetTotalEventCount();
		int count = _byActorStorage.Count;
		int count2 = _byTargetStorage.Count;
		int count3 = _nicknameStorage.Count;
		_logger.LogInformation("═══ DATA REPOSITORY STATE ═══");
		_logger.LogInformation("  Total damage events: {TotalEvents}", totalEventCount);
		_logger.LogInformation("  Unique actors: {ActorCount}", count);
		_logger.LogInformation("  Unique targets: {TargetCount}", count2);
		_logger.LogInformation("  Nicknames stored: {NicknameCount}", count3);
		if (count3 > 0)
		{
			_logger.LogInformation("  Nickname mappings:");
			foreach (var (num2, text2) in _nicknameStorage.OrderBy((KeyValuePair<int, string> kvp) => kvp.Key))
			{
				int num3 = (_byActorStorage.TryGetValue(num2, out ConcurrentQueue<DamageEvent> value) ? value.Count : 0);
				_logger.LogInformation("    Player {PlayerId} = '{Nickname}' ({DamageEvents} damage events)", num2, text2, num3);
			}
		}
		_logger.LogInformation("═══════════════════════════════");
	}
}
