using System;
using System.Collections.Generic;
using System.Linq;
using AionFlex.Core.Combat;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using Microsoft.Extensions.Logging;

namespace AionFlex.Application.DpsCalculation;

public sealed class DpsCalculator : IDpsCalculator
{
	private readonly ILogger<DpsCalculator> _logger;

	private readonly IDataRepository _dataRepository;

	private readonly ISkillRepository _skillRepository;

	private readonly IMobRepository _mobRepository;

	private readonly HashSet<int> _warnedFilteredEntities = new HashSet<int>();

	private readonly Dictionary<int, TargetStatistics> _targetInfoMap = new Dictionary<int, TargetStatistics>();

	private bool _bossOnlyMode;

	private Func<int, uint>? _maxHpProvider;

	private Func<int, long, long?>? _trustedMaxHpProvider;

	private const double MinBossHpDamageCoverage = 0.9;

	private const uint BossTierHpFloor = 500000u;

	private const int MinValidEntityId = 100;

	private Func<int, HpProgress?>? _hpProgressProvider;

	private readonly HashSet<(int Target, int Life, ContributionDoubt Doubt)> _warnedUnreliableTargets = new HashSet<(int, int, ContributionDoubt)>();

	private int _currentTarget;

	private int _bossModeTarget;

	private long _lastLocalHitTime = -1L;

	private const long TargetSwitchStaleMs = 10000L;

	private const long TargetSelectionWindowMs = 5000L;

	private const long AllModeEncounterGapMs = 60000L;

	private readonly Dictionary<int, int?> _inferenceCache = new Dictionary<int, int?>();

	private long _lastSkillDiagTicks;

	private long _lastEventVersion = -1L;

	private DpsSnapshot _cachedSnapshot = new DpsSnapshot();

	private long _lastDamageTakenEventVersion = -1L;

	private DpsSnapshot _cachedDamageTakenSnapshot = new DpsSnapshot();

	private static readonly int[] PossibleOffsets = new int[26]
	{
		0, 10, 20, 30, 40, 50, 120, 130, 140, 150,
		230, 240, 250, 340, 350, 450, 1230, 1240, 1250, 1340,
		1350, 1450, 2340, 2350, 2450, 3450
	};

	private HashSet<int>? _cachedTargetIds;

	private long _lastHealingVersion = -1L;

	private HealingFightWindow _lastHealingWindow;

	private DpsSnapshot _cachedHealingSnapshot = new DpsSnapshot();

	private readonly Dictionary<int, int> _lastTargetEventCount = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _targetLifeGeneration = new Dictionary<int, int>();

	private long _lastLifeRebaseVersion;

	public Func<long>? ClockOverrideMs { get; set; }

	public bool IsBossOnlyMode => _bossOnlyMode;

	public void SetMaxHpProvider(Func<int, uint>? provider)
	{
		_maxHpProvider = provider;
	}

	public void SetTrustedMaxHpProvider(Func<int, long, long?>? provider)
	{
		_trustedMaxHpProvider = provider;
	}

	public void SetHpProgressProvider(Func<int, HpProgress?>? provider)
	{
		_hpProgressProvider = provider;
		_lastEventVersion = -1L;
	}

	private HpProgress? SafeHpProgress(int entityId)
	{
		Func<int, HpProgress?> hpProgressProvider = _hpProgressProvider;
		if (hpProgressProvider == null || entityId <= 0)
		{
			return null;
		}
		try
		{
			return hpProgressProvider(entityId);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "HP-progress provider threw for target {TargetId}", entityId);
			return null;
		}
	}

	private long NowMs()
	{
		return ClockOverrideMs?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	public void SetBossOnlyMode(bool bossOnly)
	{
		if (_bossOnlyMode == bossOnly)
		{
			_logger.LogTrace("\ud83c\udfaf SetBossOnlyMode({Mode}) — no change", bossOnly ? "Boss" : "All");
			return;
		}
		bool bossOnlyMode = _bossOnlyMode;
		_bossOnlyMode = bossOnly;
		if (!bossOnly)
		{
			_bossModeTarget = _currentTarget;
		}
		else if (_bossModeTarget != 0)
		{
			_currentTarget = _bossModeTarget;
			_dataRepository.SetCurrentTarget(_currentTarget);
			_bossModeTarget = 0;
		}
		_lastEventVersion = -1L;
		_logger.LogInformation("\ud83c\udfaf BossOnlyMode: {Old} → {New}", bossOnlyMode ? "Boss" : "All", bossOnly ? "Boss" : "All");
	}

	public DpsCalculator(ILogger<DpsCalculator> logger, IDataRepository dataRepository, ISkillRepository skillRepository, IMobRepository mobRepository)
	{
		_logger = logger;
		_dataRepository = dataRepository;
		_skillRepository = skillRepository;
		_mobRepository = mobRepository;
	}

	public static bool IsLikelyPhantomSummon(PlayerStatistics ps, int entityId)
	{
		if (ps.Job == JobClass.Unknown)
		{
			return false;
		}
		if (ps.Nickname != entityId.ToString())
		{
			return false;
		}
		if (ps.TotalHits > 1)
		{
			return false;
		}
		return ps.SkillBreakdown.Keys.Select((int c) => c / 10000).Distinct().Count() <= 1;
	}

	public static bool IsSummonSkillOnlyEntity(PlayerStatistics ps, int entityId, Func<int, bool> isSummonCarried)
	{
		if (ps.Nickname != entityId.ToString())
		{
			return false;
		}
		if (ps.SkillBreakdown.Count == 0)
		{
			return false;
		}
		foreach (int key in ps.SkillBreakdown.Keys)
		{
			if (!isSummonCarried(key))
			{
				return false;
			}
		}
		return true;
	}

	public DpsSnapshot CalculateDps()
	{
		long eventVersion = _dataRepository.GetEventVersion();
		if (eventVersion == _lastEventVersion && _lastEventVersion >= 0)
		{
			HashSet<int> cachedTargetIds = _cachedTargetIds;
			if (cachedTargetIds != null && cachedTargetIds.Count > 0 && ContributionProgressMoved(_cachedSnapshot))
			{
				_cachedSnapshot = ComputeSnapshotForTargetSet(cachedTargetIds, _cachedSnapshot.TargetName, _cachedSnapshot.TargetMode);
			}
			return _cachedSnapshot;
		}
		_lastEventVersion = eventVersion;
		_cachedTargetIds = null;
		UpdateTargetStatistics();
		if (!_bossOnlyMode)
		{
			return _cachedSnapshot = ComputeAllTargetsSnapshot();
		}
		(HashSet<int> targetIds, string targetName, int trackingTargetId) tuple = DecideTarget();
		HashSet<int> item = tuple.targetIds;
		string item2 = tuple.targetName;
		int item3 = tuple.trackingTargetId;
		_currentTarget = item3;
		_dataRepository.SetCurrentTarget(_currentTarget);
		DpsSnapshot dpsSnapshot = new DpsSnapshot
		{
			TargetName = item2
		};
		if (item.Count == 0)
		{
			_logger.LogDebug("No targets found, returning empty snapshot");
			_cachedSnapshot = dpsSnapshot;
			return dpsSnapshot;
		}
		dpsSnapshot = ComputeSnapshotForTargetSet(item, item2, TargetSelectionMode.MostRecent);
		_logger.LogTrace("DPS calc: {PlayerCount} players, {TotalDamage:N0} damage, {BattleTime}ms", dpsSnapshot.PlayerCount, dpsSnapshot.TotalDamage, dpsSnapshot.BattleTimeMs);
		_cachedSnapshot = dpsSnapshot;
		_cachedTargetIds = item;
		return dpsSnapshot;
	}

	private bool ContributionProgressMoved(DpsSnapshot snapshot)
	{
		if (snapshot.ContributionTargetId > 0 && _hpProgressProvider != null)
		{
			HpProgress? hpProgress = SafeHpProgress(snapshot.ContributionTargetId);
			HpProgress? contributionProgress = snapshot.ContributionProgress;
			if (hpProgress.HasValue != contributionProgress.HasValue)
			{
				return true;
			}
			if (!hpProgress.HasValue)
			{
				return false;
			}
			return hpProgress.GetValueOrDefault() != contributionProgress.GetValueOrDefault();
		}
		return false;
	}

	public DpsSnapshot CalculateDpsForTarget(int targetId)
	{
		UpdateTargetStatistics();
		string targetName = _dataRepository.GetMobName(targetId) ?? $"Target #{targetId}";
		if (targetId <= 0 || !_targetInfoMap.ContainsKey(targetId))
		{
			return new DpsSnapshot
			{
				TargetName = targetName
			};
		}
		return ComputeSnapshotForTargetSet(new HashSet<int> { targetId }, targetName, TargetSelectionMode.MostRecent);
	}

	public DpsSnapshot CalculateDamageTaken()
	{
		long eventVersion = _dataRepository.GetEventVersion();
		if (eventVersion == _lastDamageTakenEventVersion && _lastDamageTakenEventVersion >= 0)
		{
			return _cachedDamageTakenSnapshot;
		}
		_lastDamageTakenEventVersion = eventVersion;
		DpsSnapshot dpsSnapshot = new DpsSnapshot
		{
			TargetName = "Damage Taken",
			TargetMode = TargetSelectionMode.AllTargets
		};
		int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
		string localPlayerName = _dataRepository.GetLocalPlayerName();
		List<DamageEvent> list = new List<DamageEvent>();
		foreach (int allTargetId in _dataRepository.GetAllTargetIds())
		{
			bool flag = false;
			if (localPlayerEntityId.HasValue && allTargetId == localPlayerEntityId.Value)
			{
				flag = true;
			}
			if (!flag && localPlayerName != null && _dataRepository.GetNickname(allTargetId) == localPlayerName)
			{
				flag = true;
			}
			if (!flag)
			{
				continue;
			}
			foreach (DamageEvent item4 in _dataRepository.GetDamageEventsByTarget(allTargetId))
			{
				if (!item4.IsHeal && _dataRepository.GetNickname(item4.ActorId) == null)
				{
					list.Add(item4);
				}
			}
			_logger.LogDebug("DamageTaken: target {TargetId} is local player, {Count} NPC damage events", allTargetId, list.Count);
		}
		if (list.Count == 0)
		{
			if (localPlayerEntityId.HasValue || localPlayerName != null)
			{
				_logger.LogDebug("DamageTaken: no NPC damage events targeting local player (entityId={Id}, name={Name})", localPlayerEntityId?.ToString() ?? "null", localPlayerName ?? "null");
			}
			_cachedDamageTakenSnapshot = dpsSnapshot;
			return dpsSnapshot;
		}
		long num = long.MaxValue;
		long num2 = long.MinValue;
		foreach (DamageEvent item5 in list)
		{
			if (item5.Timestamp < num)
			{
				num = item5.Timestamp;
			}
			if (item5.Timestamp > num2)
			{
				num2 = item5.Timestamp;
			}
		}
		long activeTimeMs = (dpsSnapshot.BattleTimeMs = num2 - num);
		long num4 = 0L;
		foreach (DamageEvent item6 in list)
		{
			num4 += item6.TotalDamage;
			int actorId = item6.ActorId;
			string nickname = _dataRepository.GetMobName(actorId) ?? _dataRepository.GetNickname(actorId) ?? _dataRepository.GetNicknameWithLocalPlayerFallback(actorId) ?? $"Entity #{actorId}";
			if (!dpsSnapshot.PlayerStats.ContainsKey(actorId))
			{
				dpsSnapshot.PlayerStats[actorId] = new PlayerStatistics(actorId, nickname)
				{
					Job = JobClass.Unknown
				};
			}
			PlayerStatistics playerStatistics = dpsSnapshot.PlayerStats[actorId];
			string skillName = _skillRepository.GetSkillName(item6.SkillCode);
			playerStatistics.ProcessDamageEvent(item6, skillName);
		}
		foreach (var (entityId, playerStatistics3) in dpsSnapshot.PlayerStats)
		{
			playerStatistics3.CalculateDps(activeTimeMs);
			playerStatistics3.CalculateContribution(num4, num4);
			playerStatistics3.CalculateSkillContributions(activeTimeMs);
			(int GearScore, int CombatPower, int Level) playerStats = _dataRepository.GetPlayerStats(entityId);
			int item = playerStats.GearScore;
			int item2 = playerStats.CombatPower;
			int item3 = playerStats.Level;
			playerStatistics3.GearScore = item;
			playerStatistics3.CombatPower = item2;
			playerStatistics3.Level = item3;
		}
		dpsSnapshot.ContributionDenominator = num4;
		dpsSnapshot.ContributionBasis = ContributionBasis.PartyDamage;
		_logger.LogInformation("\ud83d\udcca DAMAGE TAKEN: {Count} attackers, {Damage:N0} total damage, {Events} events", dpsSnapshot.PlayerCount, num4, list.Count);
		_cachedDamageTakenSnapshot = dpsSnapshot;
		return dpsSnapshot;
	}

	public DpsSnapshot CalculateHealing(HealingFightWindow window)
	{
		long healEventVersion = _dataRepository.GetHealEventVersion();
		if (healEventVersion == _lastHealingVersion && window == _lastHealingWindow)
		{
			return _cachedHealingSnapshot;
		}
		_lastHealingVersion = healEventVersion;
		_lastHealingWindow = window;
		_cachedHealingSnapshot = HealingSnapshotBuilder.Build(_dataRepository.GetHealEvents(), (int healerId) => _dataRepository.GetNickname(healerId) ?? _dataRepository.GetNicknameWithLocalPlayerFallback(healerId) ?? healerId.ToString(), _skillRepository.GetSkillName, window);
		return _cachedHealingSnapshot;
	}

	public IReadOnlyDictionary<int, DpsSnapshot> GetPerTargetSnapshots()
	{
		UpdateTargetStatistics();
		int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
		Dictionary<int, DpsSnapshot> dictionary = new Dictionary<int, DpsSnapshot>();
		foreach (int key in _targetInfoMap.Keys)
		{
			if ((!localPlayerEntityId.HasValue || key != localPlayerEntityId.Value) && _dataRepository.GetNickname(key) == null)
			{
				DpsSnapshot dpsSnapshot = ComputeSnapshotForTargetSet(new HashSet<int> { key }, $"Target #{key}", TargetSelectionMode.MostRecent);
				if (dpsSnapshot.PlayerCount > 0)
				{
					dictionary[key] = dpsSnapshot;
				}
			}
		}
		return dictionary;
	}

	private (long Denominator, ContributionBasis Basis) ResolveContributionBasis(IReadOnlyDictionary<int, long> damagePerTarget, long coverageTotal, long partyDamage, int primaryTargetId)
	{
		Func<int, long, long?> trustedMaxHpProvider = _trustedMaxHpProvider;
		if (trustedMaxHpProvider == null || coverageTotal <= 0 || primaryTargetId <= 0)
		{
			return (Denominator: partyDamage, Basis: ContributionBasis.PartyDamage);
		}
		if (ContributionPolicy.UsesPartyDamageBasis(DungeonCategoryOf(primaryTargetId)))
		{
			return (Denominator: partyDamage, Basis: ContributionBasis.PartyDamage);
		}
		long? num;
		try
		{
			num = trustedMaxHpProvider(primaryTargetId, GetTargetFirstDamageTime(primaryTargetId));
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Trusted-MaxHp provider threw for target {TargetId}", primaryTargetId);
			return (Denominator: partyDamage, Basis: ContributionBasis.PartyDamage);
		}
		if (!num.HasValue || num.GetValueOrDefault() <= 0)
		{
			return (Denominator: partyDamage, Basis: ContributionBasis.PartyDamage);
		}
		if ((double)damagePerTarget[primaryTargetId] < (double)coverageTotal * 0.9)
		{
			return (Denominator: partyDamage, Basis: ContributionBasis.PartyDamage);
		}
		return (Denominator: num.Value, Basis: ContributionBasis.BossHp);
	}

	private void ApplyContributionCheck(DpsSnapshot snapshot, int primaryTargetId, HpProgress? progress, Dictionary<(int Actor, int Target), long> asOfByActor)
	{
		snapshot.ContributionTargetId = primaryTargetId;
		snapshot.ContributionProgress = progress;
		if (snapshot.ContributionBasis != ContributionBasis.BossHp)
		{
			snapshot.ContributionLineApplies = ContributionPolicy.PartyShareLineApplies(progress);
			return;
		}
		long num = 0L;
		foreach (int key in snapshot.PlayerStats.Keys)
		{
			num += asOfByActor.GetValueOrDefault((key, primaryTargetId));
		}
		ContributionCheck contributionCheck = ContributionPolicy.Check(snapshot.ContributionDenominator, progress, num);
		snapshot.ContributionLineApplies = contributionCheck.LineApplies;
		snapshot.ContributionDoubt = contributionCheck.Doubt;
		snapshot.ContributionMeasuredDamage = num;
		if (contributionCheck.Reliable || !progress.HasValue)
		{
			return;
		}
		HpProgress valueOrDefault = progress.GetValueOrDefault();
		if (!_warnedUnreliableTargets.Add((primaryTargetId, _dataRepository.GetEntityLifeGeneration(primaryTargetId), contributionCheck.Doubt)))
		{
			return;
		}
		long num2 = Math.Max(0L, snapshot.ContributionDenominator - valueOrDefault.MinAliveHp);
		(string, string, int, long) tuple = (from x in snapshot.PlayerStats.Values.SelectMany((PlayerStatistics ps) => ps.SkillBreakdown.Values.Select((SkillStatistics sk) => (Player: ps.Nickname, SkillName: sk.SkillName, SkillCode: sk.SkillCode, TotalDamage: sk.TotalDamage)))
			orderby x.TotalDamage descending
			select x).FirstOrDefault();
		_logger.LogWarning("⚠\ufe0f CONTRIBUTION UNRELIABLE ({Doubt}): {Damage:N0} damage recorded on {Target} (entity {TargetId}) up to its lowest HP reading, which says {Removed:N0} of {Bar:N0} HP is gone ({Ratio:F2}×; attempt start {AttemptStart}). Biggest skill: {Player} {Skill} ({SkillCode}) {SkillDamage:N0}. The figures show grey with a '?'.", contributionCheck.Doubt, num, _dataRepository.GetMobName(primaryTargetId) ?? $"Target #{primaryTargetId}", primaryTargetId, num2, snapshot.ContributionDenominator, (num2 > 0) ? ((double)num / (double)num2) : 0.0, valueOrDefault.AttemptStartMs, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
	}

	private static void MoveActorDamage(Dictionary<(int Actor, int Target), long> byActor, int from, int to)
	{
		if (byActor.Count == 0)
		{
			return;
		}
		foreach (var item in byActor.Keys.Where(((int Actor, int Target) k) => k.Actor == from).ToList())
		{
			(int, int) key = (to, item.Target);
			byActor[key] = byActor.GetValueOrDefault(key) + byActor[item];
			byActor.Remove(item);
		}
	}

	private string? DungeonCategoryOf(int entityId)
	{
		int entityNpcId = _dataRepository.GetEntityNpcId(entityId);
		if (entityNpcId <= 0)
		{
			return null;
		}
		return _mobRepository.GetDungeonCategory(_mobRepository.GetMobDungeon(entityNpcId));
	}

	private DpsSnapshot ComputeSnapshotForTargetSet(HashSet<int> targetIds, string targetName, TargetSelectionMode mode)
	{
		DpsSnapshot dpsSnapshot = new DpsSnapshot
		{
			TargetName = targetName,
			TargetMode = mode
		};
		(long StartMs, long EndMs) tuple = CalculateAllTargetsBattleWindow(targetIds);
		long item = tuple.StartMs;
		long item2 = tuple.EndMs;
		long num = item2 - item;
		if (num == 0L)
		{
			_logger.LogDebug("Battle time is 0 for target set, returning empty snapshot");
			return dpsSnapshot;
		}
		dpsSnapshot.BattleTimeMs = num;
		Dictionary<int, HpProgress?> dictionary = new Dictionary<int, HpProgress?>();
		foreach (int targetId in targetIds)
		{
			dictionary[targetId] = SafeHpProgress(targetId);
		}
		List<DamageEvent> list = CollectAllTargetDamageEvents(targetIds);
		if (list.Count == 0)
		{
			_logger.LogDebug("No damage events found for target set");
			return dpsSnapshot;
		}
		Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
		foreach (DamageEvent item8 in list)
		{
			int? summonerAt = _dataRepository.GetSummonerAt(item8.ActorId, item8.Timestamp);
			if (summonerAt.HasValue && item8.Unknown > 0)
			{
				dictionary2.TryAdd(item8.Unknown, summonerAt.Value);
			}
		}
		long num2 = 0L;
		Dictionary<int, long> dictionary3 = new Dictionary<int, long>();
		bool flag = targetIds.Count > 1;
		Dictionary<(int, int), long> dictionary4 = new Dictionary<(int, int), long>();
		Dictionary<(int, int), long> dictionary5 = new Dictionary<(int, int), long>();
		Dictionary<int, Dictionary<JobClass, int>> dictionary6 = new Dictionary<int, Dictionary<JobClass, int>>();
		long num3 = item2;
		int key;
		foreach (DamageEvent item9 in list)
		{
			if (item9.Timestamp > num3)
			{
				num3 = item9.Timestamp;
			}
			IDataRepository dataRepository = _dataRepository;
			int actorId = item9.ActorId;
			long? timestampMs = item9.Timestamp;
			int num4 = SummonOwnerResolver.Resolve(dataRepository, actorId, null, timestampMs);
			if (num4 == item9.ActorId && item9.Unknown > 0 && dictionary2.TryGetValue(item9.Unknown, out var value) && !_dataRepository.IsConfirmedPlayer(item9.ActorId))
			{
				int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
				key = item9.ActorId;
				if (localPlayerEntityId != key)
				{
					num4 = value;
				}
			}
			string nickname = _dataRepository.GetNicknameAt(num4, item9.Timestamp) ?? _dataRepository.GetNickname(num4) ?? _dataRepository.GetNicknameWithLocalPlayerFallback(num4) ?? num4.ToString();
			if (!dpsSnapshot.PlayerStats.ContainsKey(num4))
			{
				dpsSnapshot.PlayerStats[num4] = new PlayerStatistics(num4, nickname);
			}
			PlayerStatistics playerStatistics = dpsSnapshot.PlayerStats[num4];
			int num5 = InferOriginalSkillCode(item9.SkillCode) ?? item9.SkillCode;
			string skillName = _skillRepository.GetSkillName(num5);
			SkillRole skillRole = (item9.IsHeal ? SkillRole.Heal : SkillRole.Dps);
			DamageEvent damageEvent = item9;
			if (item9.IsPassive && item9.Damage <= 1 && playerStatistics.SkillBreakdown.TryGetValue(num5, out SkillStatistics value2) && value2.Times > 0)
			{
				int num6 = (int)(value2.DamageAmount / value2.Times);
				if (num6 > 0)
				{
					damageEvent = new DamageEvent
					{
						Timestamp = item9.Timestamp,
						ActorId = item9.ActorId,
						TargetId = item9.TargetId,
						Damage = num6,
						SkillCode = item9.SkillCode,
						RawSkillCode = item9.RawSkillCode,
						Type = item9.Type,
						IsPassive = true
					};
				}
			}
			if (skillRole != SkillRole.Heal)
			{
				num2 += damageEvent.TotalDamage;
				dictionary3[damageEvent.TargetId] = dictionary3.GetValueOrDefault(damageEvent.TargetId) + damageEvent.TotalDamage;
				(int, int) key2 = (num4, damageEvent.TargetId);
				if (flag)
				{
					dictionary4[key2] = dictionary4.GetValueOrDefault(key2) + damageEvent.TotalDamage;
				}
				HpProgress? valueOrDefault = dictionary.GetValueOrDefault(damageEvent.TargetId);
				if (valueOrDefault.HasValue)
				{
					HpProgress valueOrDefault2 = valueOrDefault.GetValueOrDefault();
					if (valueOrDefault2.MinAliveHpTimestampMs > 0 && damageEvent.Timestamp <= valueOrDefault2.MinAliveHpTimestampMs && damageEvent.Timestamp >= valueOrDefault2.AttemptStartMs)
					{
						dictionary5[key2] = dictionary5.GetValueOrDefault(key2) + damageEvent.TotalDamage;
					}
				}
			}
			playerStatistics.ProcessDamageEvent(damageEvent, skillName, skillRole, num5);
			JobClass jobClass = JobClassExtensions.FromSkillCode(num5);
			if (jobClass != JobClass.Unknown)
			{
				if (!dictionary6.TryGetValue(num4, out var value3))
				{
					value3 = (dictionary6[num4] = new Dictionary<JobClass, int>());
				}
				value3[jobClass] = value3.GetValueOrDefault(jobClass) + 1;
			}
		}
		foreach (KeyValuePair<int, Dictionary<JobClass, int>> item10 in dictionary6)
		{
			item10.Deconstruct(out key, out var value4);
			int num7 = key;
			Dictionary<JobClass, int> dictionary8 = value4;
			if (!dpsSnapshot.PlayerStats.TryGetValue(num7, out PlayerStatistics value5))
			{
				continue;
			}
			JobClass jobClass2 = JobClass.Unknown;
			int num8 = 0;
			foreach (KeyValuePair<JobClass, int> item11 in dictionary8)
			{
				item11.Deconstruct(out var key3, out key);
				JobClass jobClass3 = key3;
				int num9 = key;
				if (num9 > num8)
				{
					jobClass2 = jobClass3;
					num8 = num9;
				}
			}
			if (jobClass2 != JobClass.Unknown)
			{
				value5.Job = jobClass2;
				_dataRepository.SetJobClass(num7, jobClass2);
			}
		}
		ComputePerSecondDpsStats(list, dpsSnapshot);
		List<int> list2 = new List<int>();
		PlayerStatistics value6;
		SkillStatistics value8;
		foreach (KeyValuePair<int, PlayerStatistics> playerStat in dpsSnapshot.PlayerStats)
		{
			playerStat.Deconstruct(out key, out value6);
			int num10 = key;
			PlayerStatistics playerStatistics2 = value6;
			if (num10 < 100 && !_dataRepository.IsConfirmedPlayer(num10))
			{
				list2.Add(num10);
				continue;
			}
			if (_dataRepository.IsConfirmedPlayer(num10))
			{
				continue;
			}
			int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
			key = num10;
			if (localPlayerEntityId == key)
			{
				continue;
			}
			int? summonerAt2 = _dataRepository.GetSummonerAt(num10, playerStatistics2.FirstHitTime);
			int? summonerAt3 = _dataRepository.GetSummonerAt(num10, playerStatistics2.LastHitTime);
			bool flag2 = summonerAt2.HasValue || summonerAt3.HasValue || _dataRepository.GetSummoner(num10).HasValue;
			int? num11;
			int? num12;
			if (flag2)
			{
				num11 = null;
				num12 = ((summonerAt2 == summonerAt3) ? summonerAt2 : ((int?)null));
				if (num12.HasValue)
				{
					if (!_dataRepository.IsConfirmedPlayer(num12.Value))
					{
						int? localPlayerEntityId2 = _dataRepository.GetLocalPlayerEntityId();
						key = num12.Value;
						if (localPlayerEntityId2 != key)
						{
							goto IL_075b;
						}
					}
					if (dpsSnapshot.PlayerStats.ContainsKey(num12.Value) && !list2.Contains(num12.Value))
					{
						num11 = num12.Value;
					}
				}
				goto IL_075b;
			}
			if (_dataRepository.GetMobName(num10) != null)
			{
				if (_warnedFilteredEntities.Add(num10))
				{
					_logger.LogInformation("✗ FILTERED known-mob {PlayerId} ('{MobName}', would-be {Job}): {Damage:N0} damage — entity is a mob/boss, not a player", num10, _dataRepository.GetMobName(num10), playerStatistics2.Job, playerStatistics2.TotalDamage);
				}
				list2.Add(num10);
			}
			else if (IsSummonSkillOnlyEntity(playerStatistics2, num10, _skillRepository.IsSummonCarriedSkill))
			{
				if (_warnedFilteredEntities.Add(num10))
				{
					string text = string.Join(", ", playerStatistics2.SkillBreakdown.Values.Select((SkillStatistics s) => $"{s.SkillName}({s.SkillCode})"));
					_logger.LogInformation("\ud83d\udc3e WITHHELD summon-skill entity {EntityId} (inferred {Job}, no name, hits={Hits}): {Damage:N0} dmg from [{Skills}] — every family is game-data-proven summon-carried; owner stamp missing, so the row is hidden rather than shown as a bare number.", num10, playerStatistics2.Job, playerStatistics2.TotalHits, playerStatistics2.TotalDamage, text);
				}
				list2.Add(num10);
			}
			else if (playerStatistics2.Job != JobClass.Unknown)
			{
				if (IsLikelyPhantomSummon(playerStatistics2, num10))
				{
					if (_warnedFilteredEntities.Add(num10))
					{
						string text2 = string.Join(", ", playerStatistics2.SkillBreakdown.Values.Select((SkillStatistics s) => $"{s.SkillName}({s.SkillCode})"));
						_logger.LogInformation("\ud83d\udc7b WITHHELD phantom summon {EntityId} (inferred {Job}, no name, hits={Hits}): {Damage:N0} dmg from [{Skills}] — boss illusion casting player-range skills. Auto-re-admits on a real name, a 2nd skill family, or a 2nd direct hit.", num10, playerStatistics2.Job, playerStatistics2.TotalHits, playerStatistics2.TotalDamage, text2);
					}
					list2.Add(num10);
				}
				else if (_warnedFilteredEntities.Add(num10))
				{
					_logger.LogInformation("✓ ACCEPTED unconfirmed player {PlayerId} ('{Nickname}', {Job}): {Damage:N0} damage — job class inferred from skill codes", num10, playerStatistics2.Nickname, playerStatistics2.Job, playerStatistics2.TotalDamage);
				}
			}
			else
			{
				if (playerStatistics2.TotalDamage >= 10000 && _warnedFilteredEntities.Add(num10))
				{
					_logger.LogWarning("⚠\ufe0f FILTERED entity {PlayerId} ('{Nickname}', {Job}): {Damage:N0} damage dropped — not confirmed as player (no 04 8D nickname packet received)", num10, playerStatistics2.Nickname, playerStatistics2.Job, playerStatistics2.TotalDamage);
				}
				else if (playerStatistics2.TotalDamage < 10000)
				{
					_logger.LogTrace("✗ FILTERED unconfirmed entity {PlayerId} ('{Nickname}', {Job}): {Damage:N0} damage, isSummon={IsSummon}", num10, playerStatistics2.Nickname, playerStatistics2.Job, playerStatistics2.TotalDamage, flag2);
				}
				list2.Add(num10);
			}
			continue;
			IL_075b:
			if (num11.HasValue && dpsSnapshot.PlayerStats.TryGetValue(num11.Value, out PlayerStatistics value7))
			{
				if (playerStatistics2.FirstHitTime < value7.FirstHitTime)
				{
					value7.FirstHitTime = playerStatistics2.FirstHitTime;
				}
				if (playerStatistics2.LastHitTime > value7.LastHitTime)
				{
					value7.LastHitTime = playerStatistics2.LastHitTime;
				}
				foreach (KeyValuePair<int, SkillStatistics> item12 in playerStatistics2.SkillBreakdown)
				{
					item12.Deconstruct(out key, out value8);
					int num13 = key;
					SkillStatistics skillStatistics = value8;
					value7.TotalDamage += skillStatistics.TotalDamage;
					value7.DpsDamage += skillStatistics.TotalDamage;
					if (!value7.SkillBreakdown.ContainsKey(num13))
					{
						value7.SkillBreakdown[num13] = new SkillStatistics(num13, skillStatistics.SkillName);
					}
					value7.SkillBreakdown[num13].MergeFrom(skillStatistics);
				}
				MoveActorDamage(dictionary4, num10, num11.Value);
				MoveActorDamage(dictionary5, num10, num11.Value);
				_logger.LogDebug("\ud83d\udd00 MERGE: entity {EntityId} ({Job}) → player {OwnerId} (summon), damage={Damage:N0}", num10, playerStatistics2.Job, num11.Value, playerStatistics2.TotalDamage);
				list2.Add(num10);
			}
			else
			{
				if (_warnedFilteredEntities.Add(num10))
				{
					_logger.LogInformation("\ud83d\ude48 HIDDEN summon {EntityId} ({Job}): {Damage:N0} damage — server-stamped summon (owner={Owner}) with no confirmed-player owner in this snapshot; never shown as a bare row", num10, playerStatistics2.Job, playerStatistics2.TotalDamage, num12.HasValue ? num12.Value : (-1));
				}
				list2.Add(num10);
			}
		}
		foreach (int item13 in list2)
		{
			dpsSnapshot.PlayerStats.Remove(item13);
		}
		long num14 = 0L;
		int num15 = 0;
		long num16 = 0L;
		long num17 = 0L;
		long num18 = 0L;
		foreach (int item14 in list2)
		{
			if (dpsSnapshot.PlayerStats.TryGetValue(item14, out PlayerStatistics value9))
			{
				if (value9.Job == JobClass.Unknown)
				{
					num16 += value9.TotalDamage;
				}
				else
				{
					num17 += value9.TotalDamage;
				}
			}
		}
		foreach (int item15 in list2)
		{
			dpsSnapshot.PlayerStats.Remove(item15);
		}
		long totalDamage = dpsSnapshot.TotalDamage;
		int num19 = ((dictionary3.Count > 0) ? dictionary3.MaxBy((KeyValuePair<int, long> kv) => kv.Value).Key : 0);
		(long Denominator, ContributionBasis Basis) tuple2 = ResolveContributionBasis(dictionary3, num2, totalDamage, num19);
		long item3 = tuple2.Denominator;
		ContributionBasis item4 = tuple2.Basis;
		dpsSnapshot.ContributionDenominator = item3;
		dpsSnapshot.ContributionBasis = item4;
		foreach (KeyValuePair<int, PlayerStatistics> playerStat2 in dpsSnapshot.PlayerStats)
		{
			playerStat2.Deconstruct(out key, out value6);
			int num20 = key;
			PlayerStatistics playerStatistics3 = value6;
			playerStatistics3.ConsolidateSkillsByName();
			long activeTimeMs = playerStatistics3.ResolveActiveTimeMs(num3, num);
			playerStatistics3.CalculateDps(activeTimeMs);
			long? contributionNumerator = (((item4 == ContributionBasis.BossHp) & flag) ? new long?(dictionary4.GetValueOrDefault((num20, num19))) : ((long?)null));
			playerStatistics3.CalculateContribution(item3, totalDamage, contributionNumerator);
			playerStatistics3.CalculateSkillContributions(activeTimeMs);
			(int GearScore, int CombatPower, int Level) playerStats = _dataRepository.GetPlayerStats(num20);
			int item5 = playerStats.GearScore;
			int item6 = playerStats.CombatPower;
			int item7 = playerStats.Level;
			playerStatistics3.GearScore = item5;
			playerStatistics3.CombatPower = item6;
			playerStatistics3.Level = item7;
			num14 += playerStatistics3.TotalDamage;
			num15++;
			string nickname2 = playerStatistics3.Nickname;
			key = playerStatistics3.PlayerId;
			if (nickname2 == key.ToString())
			{
				num18 += playerStatistics3.TotalDamage;
			}
		}
		ApplyContributionCheck(dpsSnapshot, num19, dictionary.GetValueOrDefault(num19), dictionary5);
		_logger.LogTrace("\ud83d\udcca DPS CALC: events={Events} | totalDmg={TotalDmg:N0} | players={Players} | finalDmg={FinalDmg:N0} | npcFiltered={Filtered:N0} | merged={Merged:N0} | orphan={Orphan:N0} | lost={Lost:N0}", list.Count, num2, num15, num14, num16, num17, num18, num2 - num14 - num16);
		long tickCount = Environment.TickCount64;
		if (tickCount - _lastSkillDiagTicks > 30000)
		{
			_lastSkillDiagTicks = tickCount;
			foreach (KeyValuePair<int, PlayerStatistics> playerStat3 in dpsSnapshot.PlayerStats)
			{
				playerStat3.Deconstruct(out key, out value6);
				foreach (KeyValuePair<int, SkillStatistics> item16 in value6.SkillBreakdown)
				{
					item16.Deconstruct(out key, out value8);
					int num21 = key;
					SkillStatistics skillStatistics2 = value8;
					if (skillStatistics2.SeenRawCodes.Count > 3)
					{
						string text3 = string.Join(", ", skillStatistics2.SeenRawCodes.OrderBy((int c) => c));
						_logger.LogWarning("\ud83d\udd0d SKILL GROUPING: '{SkillName}' (code={SkillCode}) has {RawCount} distinct raw codes: [{RawCodes}] — hits={Hits}+{DoT}t, damage={Damage:N0}", skillStatistics2.SkillName, num21, skillStatistics2.SeenRawCodes.Count, text3, skillStatistics2.Times, skillStatistics2.DotTimes, skillStatistics2.TotalDamage);
					}
					if (skillStatistics2.Times > 20 && skillStatistics2.DotTimes > 0 && skillStatistics2.DotTimes < skillStatistics2.Times / 10)
					{
						_logger.LogWarning("\ud83d\udd0d SKILL HIT/DOT MISMATCH: '{SkillName}' (code={SkillCode}) has {Hits} direct hits but only {DoT} DoT ticks — expected ~{Expected}+ ticks if all were real casts. Possible misattribution. RawCodes=[{RawCodes}]", skillStatistics2.SkillName, num21, skillStatistics2.Times, skillStatistics2.DotTimes, skillStatistics2.Times * 5, string.Join(", ", skillStatistics2.SeenRawCodes.OrderBy((int c) => c)));
					}
				}
			}
		}
		return dpsSnapshot;
	}

	public void Reset()
	{
		_dataRepository.FlushDamageData();
		_targetInfoMap.Clear();
		_inferenceCache.Clear();
		_lastTargetEventCount.Clear();
		_targetLifeGeneration.Clear();
		_lastEventVersion = -1L;
		_cachedSnapshot = new DpsSnapshot();
		_lastDamageTakenEventVersion = -1L;
		_cachedDamageTakenSnapshot = new DpsSnapshot();
		_lastHealingVersion = -1L;
		_cachedHealingSnapshot = new DpsSnapshot();
		_currentTarget = 0;
		_bossModeTarget = 0;
		_lastLocalHitTime = -1L;
		_warnedFilteredEntities.Clear();
		_warnedUnreliableTargets.Clear();
		_cachedTargetIds = null;
		_logger.LogInformation("DPS calculator reset");
	}

	public int GetCurrentTargetId()
	{
		return _currentTarget;
	}

	public long GetTargetFirstDamageTime(int targetId)
	{
		if (!_targetInfoMap.TryGetValue(targetId, out TargetStatistics value))
		{
			return 0L;
		}
		return value.FirstDamageTime;
	}

	public long GetTargetLastDamageTime(int targetId)
	{
		if (!_targetInfoMap.TryGetValue(targetId, out TargetStatistics value))
		{
			return 0L;
		}
		return value.LastDamageTime;
	}

	public string GetCurrentTargetDisplayName()
	{
		int currentTarget = _currentTarget;
		if (currentTarget <= 0)
		{
			return string.Empty;
		}
		return _dataRepository.GetMobName(currentTarget) ?? $"Target #{currentTarget}";
	}

	public IReadOnlyList<int> GetKnownTargetIds()
	{
		int? localPlayerId = _dataRepository.GetLocalPlayerEntityId();
		return (from id in _targetInfoMap.Keys.Where((int id) =>
			{
				if (localPlayerId.HasValue && id == localPlayerId.Value)
				{
					return false;
				}
				if (_targetInfoMap[id].TotalDamageReceived <= 0)
				{
					return false;
				}
				if (_dataRepository.GetSummoner(id).HasValue)
				{
					return false;
				}
				return (!_dataRepository.IsConfirmedPlayer(id) && _dataRepository.GetNickname(id) == null) ? true : false;
			})
			orderby _targetInfoMap[id].FirstDamageTime
			select id).ToList();
	}

	public string? GetLocalPlayerDisplayName()
	{
		int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
		if (localPlayerEntityId.HasValue)
		{
			string nickname = _dataRepository.GetNickname(localPlayerEntityId.Value);
			if (nickname != null)
			{
				return nickname;
			}
		}
		return _dataRepository.GetLocalPlayerName();
	}

	private DpsSnapshot ComputeAllTargetsSnapshot()
	{
		List<int> list = new List<int>();
		foreach (int key2 in _targetInfoMap.Keys)
		{
			if (!_dataRepository.GetSummoner(key2).HasValue && !_dataRepository.IsConfirmedPlayer(key2) && _dataRepository.GetLocalPlayerEntityId() != key2 && _dataRepository.GetNickname(key2) == null && _targetInfoMap[key2].TotalDamageReceived > 0 && !IsPlayerHealed(key2))
			{
				list.Add(key2);
			}
		}
		if (list.Count == 0)
		{
			return new DpsSnapshot
			{
				TargetName = "All Targets"
			};
		}
		long latestLast = list.Max((int id) => _targetInfoMap[id].LastDamageTime);
		int key = list.Where((int id) => _targetInfoMap[id].LastDamageTime >= latestLast - 60000).ToList().MaxBy((int id) => _targetInfoMap[id].TotalDamageReceived);
		long encounterStart = _targetInfoMap[key].FirstDamageTime;
		HashSet<int> hashSet = list.Where((int id) => _targetInfoMap[id].LastDamageTime >= encounterStart).ToHashSet();
		int currentTarget = (_currentTarget = hashSet.MaxBy((int id) => _targetInfoMap[id].TotalDamageReceived));
		_dataRepository.SetCurrentTarget(currentTarget);
		DpsSnapshot dpsSnapshot = ComputeSnapshotForTargetSet(hashSet, "All Targets", TargetSelectionMode.MostRecent);
		_cachedTargetIds = hashSet;
		_logger.LogTrace("DPS calc (ALL): {Targets}/{Cand} targets in encounter, {Players} players, {Damage:N0} damage", hashSet.Count, list.Count, dpsSnapshot.PlayerCount, dpsSnapshot.TotalDamage);
		return dpsSnapshot;
	}

	private (HashSet<int> targetIds, string targetName, int trackingTargetId) DecideTarget()
	{
		HashSet<int> hashSet = new HashSet<int>();
		foreach (int key in _targetInfoMap.Keys)
		{
			if (_dataRepository.IsConfirmedPlayer(key) || _dataRepository.GetLocalPlayerEntityId() == key || _dataRepository.GetNickname(key) != null || _targetInfoMap[key].TotalDamageReceived <= 0 || IsPlayerHealed(key))
			{
				continue;
			}
			int entityNpcId = _dataRepository.GetEntityNpcId(key);
			if (entityNpcId > 0)
			{
				string mobDungeon = _mobRepository.GetMobDungeon(entityNpcId);
				// Boss mode must count named world bosses too. The section filter
				// should only discard ordinary open-world mobs.
				string? knownRole = _mobRepository.GetMobRole(entityNpcId);
				if (mobDungeon != null && !_mobRepository.IsInstanceDungeon(mobDungeon) &&
				    knownRole != "boss" && knownRole != "miniboss")
				{
					continue;
				}
			}
			string text = ((entityNpcId > 0) ? _mobRepository.GetMobRole(entityNpcId) : null);
			bool num = text != null && !(text == "boss") && !(text == "miniboss");
			uint num2 = _maxHpProvider?.Invoke(key) ?? 0;
			bool flag = !num && num2 >= 500000;
			bool flag2 = !num && !PartyMemberEvidencePolicy.HasPlayerTierHpBar(num2) && _targetInfoMap[key].DamageAbsorbedWhileAlive >= 500000;
			bool flag3 = flag | flag2;
			if ((flag || !_dataRepository.GetSummoner(key).HasValue) && (!_bossOnlyMode || flag3 || ((text == "boss" || text == "miniboss") ? true : false)))
			{
				hashSet.Add(key);
			}
		}
		if (hashSet.Count == 0)
		{
			int count = _targetInfoMap.Count;
			if (count > 0)
			{
				List<string> list = new List<string>();
				using (Dictionary<int, TargetStatistics>.KeyCollection.Enumerator enumerator = _targetInfoMap.Keys.GetEnumerator())
				{
					int current2;
					long totalDamageReceived;
					string value;
					for (; enumerator.MoveNext(); list.Add($"{current2}(dmg={totalDamageReceived},{value})"))
					{
						current2 = enumerator.Current;
						totalDamageReceived = _targetInfoMap[current2].TotalDamageReceived;
						value = "OK";
						if (_dataRepository.GetSummoner(current2).HasValue)
						{
							value = "summon";
							continue;
						}
						if (_dataRepository.IsConfirmedPlayer(current2))
						{
							value = "confirmedPlayer";
							continue;
						}
						if (_dataRepository.GetLocalPlayerEntityId() == current2)
						{
							value = "isLocalPlayer";
							continue;
						}
						if (_dataRepository.GetNickname(current2) != null)
						{
							value = "hasNickname";
							continue;
						}
						if (totalDamageReceived <= 0)
						{
							value = "noDamage";
							continue;
						}
						if (IsPlayerHealed(current2))
						{
							value = "playerHealed";
							continue;
						}
						int entityNpcId2 = _dataRepository.GetEntityNpcId(current2);
						if (entityNpcId2 > 0)
						{
							string mobDungeon2 = _mobRepository.GetMobDungeon(entityNpcId2);
							if (mobDungeon2 != null && !_mobRepository.IsInstanceDungeon(mobDungeon2))
							{
								value = "nonInstanceNpc(" + mobDungeon2 + ")";
								continue;
							}
						}
						if (_bossOnlyMode)
						{
							int entityNpcId3 = _dataRepository.GetEntityNpcId(current2);
							string text2 = ((entityNpcId3 > 0) ? _mobRepository.GetMobRole(entityNpcId3) : null);
							if ((!(text2 == "boss") && !(text2 == "miniboss")) || 1 == 0)
							{
								uint value2 = _maxHpProvider?.Invoke(current2) ?? 0;
								long damageAbsorbedWhileAlive = _targetInfoMap[current2].DamageAbsorbedWhileAlive;
								value = $"bossOnly(role={text2 ?? "none"},maxHp={value2},absorbed={damageAbsorbedWhileAlive},floor={500000u})";
							}
						}
					}
				}
				_logger.LogWarning("\ud83c\udfaf DecideTarget: 0 candidates of {N} targets. Reasons: [{Reasons}]", count, string.Join(", ", list));
			}
			else
			{
				_logger.LogWarning("\ud83c\udfaf DecideTarget: _targetInfoMap is empty (no targets ingested via UpdateTargetStatistics). DataRepo target count: {Count}", _dataRepository.GetAllTargetIds().Count);
			}
			return (targetIds: new HashSet<int>(), targetName: "", trackingTargetId: 0);
		}
		if (hashSet.Count > 1)
		{
			long num3 = NowMs();
			foreach (int item in hashSet)
			{
				TargetStatistics targetStatistics = _targetInfoMap[item];
				string text3 = _dataRepository.GetMobName(item) ?? $"#{item}";
				long num4 = num3 - targetStatistics.LastDamageTime;
				_logger.LogDebug("TARGET CANDIDATE: {Name} (entity={Id}) dmg={Damage} lastHit={Age}ms ago", text3, item, targetStatistics.TotalDamageReceived, num4);
			}
		}
		HashSet<int> hashSet2 = ResolveLocalActorIds();
		if (hashSet2 != null && hashSet2.Count > 0)
		{
			int num5 = SelectTargetLastHitByMe(hashSet2, hashSet, _currentTarget);
			if (num5 != 0 && hashSet.Contains(num5))
			{
				string text4 = _dataRepository.GetMobName(num5) ?? $"Target #{num5}";
				if (num5 != _currentTarget && _currentTarget != 0)
				{
					string text5 = _dataRepository.GetMobName(_currentTarget) ?? $"#{_currentTarget}";
					_logger.LogInformation("TARGET SWITCH: {OldName} (entity={OldId}) → {NewName} (entity={NewId})", text5, _currentTarget, text4, num5);
				}
				else if (num5 == _currentTarget && hashSet.Count > 1)
				{
					long num6 = NowMs();
					TargetStatistics valueOrDefault = _targetInfoMap.GetValueOrDefault(num5);
					long num7 = ((valueOrDefault != null) ? (num6 - valueOrDefault.LastDamageTime) : (-1));
					if (num7 > 5000)
					{
						_logger.LogWarning("TARGET STUCK: staying on {Name} (entity={Id}) but last hit was {Age}ms ago. Candidates: {Count}. wasIdle={WasIdle}, lastLocalHit={LastHit}ms ago", text4, num5, num7, hashSet.Count, _lastLocalHitTime < 0 || num6 - _lastLocalHitTime > 5000, (_lastLocalHitTime < 0) ? (-1) : (num6 - _lastLocalHitTime));
					}
				}
				return (targetIds: new HashSet<int> { num5 }, targetName: text4, trackingTargetId: num5);
			}
		}
		int num8 = hashSet.MaxBy((int id) => _targetInfoMap[id].TotalDamageReceived);
		int num9 = hashSet.MaxBy((int id) => _targetInfoMap[id].LastDamageTime);
		int num10 = (ShouldPreferMostRecent(num8, num9) ? num9 : num8);
		string text6 = _dataRepository.GetMobName(num10) ?? $"Target #{num10}";
		_logger.LogWarning("TARGET FALLBACK: selected {Name} (entity={Id}) via {Method}", text6, num10, (num10 == num9) ? "MOST_RECENT" : "MOST_DAMAGE");
		return (targetIds: new HashSet<int> { num10 }, targetName: text6, trackingTargetId: num10);
	}

	private bool ShouldPreferMostRecent(int mostDamage, int mostRecent)
	{
		if (mostDamage == 0 || mostRecent == 0 || mostDamage == mostRecent)
		{
			return false;
		}
		if (!_targetInfoMap.TryGetValue(mostDamage, out TargetStatistics value))
		{
			return false;
		}
		if (!_targetInfoMap.TryGetValue(mostRecent, out TargetStatistics value2))
		{
			return false;
		}
		long num = NowMs();
		bool flag = num - value.LastDamageTime >= 10000;
		bool flag2 = num - value2.LastDamageTime < 10000;
		return flag & flag2;
	}

	private int SelectTargetLastHitByMe(HashSet<int> localActorIds, HashSet<int> candidates, int fallbackTarget)
	{
		long now = NowMs();
		long num = now - 5000;
		bool flag = _lastLocalHitTime < 0 || now - _lastLocalHitTime > 5000;
		int num2 = fallbackTarget;
		long num3 = -1L;
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		Dictionary<int, long> recentTimes = new Dictionary<int, long>();
		foreach (int localActorId in localActorIds)
		{
			foreach (DamageEvent item in _dataRepository.GetDamageEventsByActor(localActorId))
			{
				if (item.TargetId <= 0 || item.TotalDamage <= 0 || _dataRepository.IsConfirmedPlayer(item.TargetId) || _dataRepository.GetNickname(item.TargetId) != null || !candidates.Contains(item.TargetId))
				{
					continue;
				}
				if (item.Timestamp > num3)
				{
					num3 = item.Timestamp;
					num2 = item.TargetId;
				}
				if (item.Timestamp >= num)
				{
					dictionary[item.TargetId] = dictionary.GetValueOrDefault(item.TargetId) + item.TotalDamage;
					if (item.Timestamp > recentTimes.GetValueOrDefault(item.TargetId))
					{
						recentTimes[item.TargetId] = item.Timestamp;
					}
				}
			}
		}
		if (num3 < 0)
		{
			if (ShouldDeferToPartyFallback())
			{
				return 0;
			}
			return fallbackTarget;
		}
		if (flag && num3 > _lastLocalHitTime)
		{
			_lastLocalHitTime = num3;
			return num2;
		}
		if (num3 <= _lastLocalHitTime && fallbackTarget != 0)
		{
			if (ShouldDeferToPartyFallback())
			{
				return 0;
			}
			return fallbackTarget;
		}
		int result = ((dictionary.Count > 0) ? (from kv in dictionary
			orderby kv.Value descending, recentTimes.GetValueOrDefault(kv.Key) descending
			select kv).First().Key : num2);
		_lastLocalHitTime = num3;
		return result;
		bool ShouldDeferToPartyFallback()
		{
			if (fallbackTarget == 0)
			{
				return false;
			}
			if (!_targetInfoMap.TryGetValue(fallbackTarget, out TargetStatistics value))
			{
				return false;
			}
			if (now - value.LastDamageTime < 10000)
			{
				return false;
			}
			foreach (int candidate in candidates)
			{
				if (candidate != fallbackTarget && _targetInfoMap.TryGetValue(candidate, out TargetStatistics value2) && now - value2.LastDamageTime < 5000)
				{
					_logger.LogInformation("\ud83c\udfaf PARTY-MODE FALLBACK: local idle (last={LastHit}ms), current={Old} stale ({OldAge}ms) → defer to party fallback (fresh candidate {New} hit {NewAge}ms ago)", (_lastLocalHitTime < 0) ? (-1) : (now - _lastLocalHitTime), fallbackTarget, now - value.LastDamageTime, candidate, now - value2.LastDamageTime);
					return true;
				}
			}
			return false;
		}
	}

	private bool IsPlayerHealed(int id)
	{
		int? localId = _dataRepository.GetLocalPlayerEntityId();
		return _targetInfoMap[id].ReceivedPlayerHeal((int actor) => actor == localId || _dataRepository.IsConfirmedPlayer(actor), _dataRepository.GetEntityAppearanceStartMs(id));
	}

	private HashSet<int>? ResolveLocalActorIds()
	{
		HashSet<int> hashSet = new HashSet<int>();
		int? localPlayerEntityId = _dataRepository.GetLocalPlayerEntityId();
		if (localPlayerEntityId.HasValue)
		{
			hashSet.Add(localPlayerEntityId.Value);
		}
		string text = _dataRepository.GetLocalPlayerName()?.Trim().ToLowerInvariant();
		if (text != null)
		{
			foreach (var (item, text3) in _dataRepository.GetAllNicknames())
			{
				if (text3.Trim().ToLowerInvariant() == text)
				{
					hashSet.Add(item);
				}
			}
		}
		if (hashSet.Count == 0)
		{
			return null;
		}
		foreach (int key in _targetInfoMap.Keys)
		{
			int? summoner = _dataRepository.GetSummoner(key);
			if (summoner.HasValue && hashSet.Contains(summoner.Value))
			{
				hashSet.Add(key);
			}
		}
		foreach (int allActorId in _dataRepository.GetAllActorIds())
		{
			int? summoner2 = _dataRepository.GetSummoner(allActorId);
			if (summoner2.HasValue && hashSet.Contains(summoner2.Value))
			{
				hashSet.Add(allActorId);
			}
		}
		return hashSet;
	}

	private void UpdateTargetStatistics()
	{
		long entityLifeRebaseVersion = _dataRepository.GetEntityLifeRebaseVersion();
		if (entityLifeRebaseVersion != _lastLifeRebaseVersion)
		{
			foreach (int item in _lastTargetEventCount.Keys.ToList())
			{
				if (_dataRepository.GetEntityLifeGeneration(item) != _targetLifeGeneration.GetValueOrDefault(item))
				{
					ForgetTarget(item);
				}
			}
			_lastLifeRebaseVersion = entityLifeRebaseVersion;
		}
		foreach (int allTargetId in _dataRepository.GetAllTargetIds())
		{
			int entityLifeGeneration = _dataRepository.GetEntityLifeGeneration(allTargetId);
			if (entityLifeGeneration != _targetLifeGeneration.GetValueOrDefault(allTargetId))
			{
				ForgetTarget(allTargetId);
			}
			IReadOnlyCollection<DamageEvent> damageEventsByTarget = _dataRepository.GetDamageEventsByTarget(allTargetId);
			int count = damageEventsByTarget.Count;
			bool flag = _lastTargetEventCount.TryGetValue(allTargetId, out var value);
			if (flag && count == value)
			{
				continue;
			}
			if (flag && count < value && _targetInfoMap.TryGetValue(allTargetId, out TargetStatistics value2))
			{
				value2.PruneProcessedTo(damageEventsByTarget);
			}
			_lastTargetEventCount[allTargetId] = count;
			_targetLifeGeneration[allTargetId] = entityLifeGeneration;
			foreach (DamageEvent item2 in damageEventsByTarget)
			{
				if (!_targetInfoMap.ContainsKey(allTargetId))
				{
					_targetInfoMap[allTargetId] = new TargetStatistics(allTargetId, item2.Timestamp);
				}
				_targetInfoMap[allTargetId].ProcessDamageEvent(item2);
			}
		}
	}

	private void ForgetTarget(int targetId)
	{
		_targetInfoMap.Remove(targetId);
		_lastTargetEventCount.Remove(targetId);
		_targetLifeGeneration.Remove(targetId);
	}

	private (long StartMs, long EndMs) CalculateAllTargetsBattleWindow(HashSet<int> targetIds)
	{
		List<TargetStatistics> list = (from id in targetIds
			where _targetInfoMap.ContainsKey(id)
			select _targetInfoMap[id]).ToList();
		if (list.Count == 0)
		{
			return (StartMs: 0L, EndMs: 0L);
		}
		return (StartMs: list.Min((TargetStatistics t) => t.FirstDamageTime), EndMs: list.Max((TargetStatistics t) => t.LastDamageTime));
	}

	private List<DamageEvent> CollectAllTargetDamageEvents(HashSet<int> targetIds)
	{
		int num = 0;
		foreach (int targetId in targetIds)
		{
			num += _dataRepository.GetDamageEventCountByTarget(targetId);
		}
		List<DamageEvent> list = new List<DamageEvent>(num);
		foreach (int targetId2 in targetIds)
		{
			foreach (DamageEvent item in _dataRepository.GetDamageEventsByTarget(targetId2))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private void ComputePerSecondDpsStats(List<DamageEvent> damageEvents, DpsSnapshot snapshot)
	{
		if (damageEvents.Count == 0 || snapshot.BattleTimeMs <= 0)
		{
			return;
		}
		long num = long.MaxValue;
		long num2 = long.MinValue;
		foreach (DamageEvent damageEvent in damageEvents)
		{
			if (damageEvent.Timestamp < num)
			{
				num = damageEvent.Timestamp;
			}
			if (damageEvent.Timestamp > num2)
			{
				num2 = damageEvent.Timestamp;
			}
		}
		int num3 = (int)((num2 - num) / 1000) + 1;
		if (num3 <= 0)
		{
			return;
		}
		long[] array = new long[num3];
		foreach (DamageEvent damageEvent2 in damageEvents)
		{
			int num4 = (int)((damageEvent2.Timestamp - num) / 1000);
			if (num4 >= 0 && num4 < num3)
			{
				array[num4] += damageEvent2.TotalDamage;
			}
		}
		long num5 = long.MaxValue;
		long num6 = long.MinValue;
		long num7 = 0L;
		int num8 = 0;
		long[] array2 = array;
		foreach (long num9 in array2)
		{
			if (num9 > 0)
			{
				num8++;
				if (num9 < num5)
				{
					num5 = num9;
				}
				if (num9 > num6)
				{
					num6 = num9;
				}
				num7 += num9;
			}
		}
		snapshot.MinDps = ((num5 == long.MaxValue) ? 0 : num5);
		snapshot.MaxDps = ((num6 == long.MinValue) ? 0 : num6);
		snapshot.AvgDps = ((num8 > 0) ? ((double)num7 / (double)num8) : 0.0);
	}

	private int? InferOriginalSkillCode(int skillCode)
	{
		if (_inferenceCache.TryGetValue(skillCode, out var value))
		{
			return value;
		}
		int[] sortedSkillCodes = _skillRepository.GetSortedSkillCodes();
		int[] possibleOffsets = PossibleOffsets;
		foreach (int num in possibleOffsets)
		{
			int value2 = skillCode - num;
			if (Array.BinarySearch(sortedSkillCodes, value2) >= 0)
			{
				_inferenceCache[skillCode] = value2;
				return value2;
			}
		}
		_inferenceCache[skillCode] = null;
		return null;
	}
}
