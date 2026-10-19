using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IDataRepository
{
	long RetentionWindowMs { get; set; }

	event EventHandler<string?>? ClientRegionChanged;

	void AppendDamageEvent(DamageEvent damageEvent);

	void AppendHealEvent(DamageEvent healEvent);

	IReadOnlyList<DamageEvent> GetHealEvents();

	long GetHealEventVersion();

	IReadOnlyCollection<DamageEvent> GetDamageEventsByActor(int actorId);

	IReadOnlyCollection<DamageEvent> GetDamageEventsByTarget(int targetId);

	int GetDamageEventCountByTarget(int targetId);

	IReadOnlyCollection<int> GetAllActorIds();

	IReadOnlyCollection<int> GetAllTargetIds();

	void SetNickname(int playerId, string nickname, bool isConfirmedPlayer = false, bool isAuthoritativeSource = false);

	string? GetNickname(int playerId);

	string? GetNicknameAt(int playerId, long timestampMs);

	bool IsConfirmedPlayer(int entityId);

	IReadOnlyDictionary<int, string> GetAllNicknames();

	void RegisterSummon(int summonId, int summonerId);

	int? GetSummoner(int summonId);

	void EndSummonLife(int summonId, long endMs, int? lifeOwnerId = null);

	int? GetSummonerAt(int summonId, long timestampMs);

	int GetSummonLifeGeneration(int summonId);

	bool IsPresentConfirmedPlayer(int entityId)
	{
		return false;
	}

	void SetEntityFingerprint(int entityId, string fingerprint);

	void SetLocalPlayerName(string characterName);

	string? GetLocalPlayerName();

	void SetCurrentServerHost(string? host);

	string? GetCurrentServerHost();

	void SetCurrentServerId(int? serverId);

	int? GetCurrentServerId();

	void RecordServerClockReading(TimeSpan offset, int? namedServerId, long nowMs);

	void ConfirmServerClockServer(int serverId, long nowMs);

	(GameRegion Region, TimeSpan Offset, long SinceMs)? GetServerClock();

	bool TryTakeStableServerClock(long nowMs, out GameRegion region, out TimeSpan offset);

	void SetClientRegion(string? regionCode);

	void SetClientInstallRegions(IReadOnlyList<string?> regionCodes);

	string? GetClientRegion();

	int? GetLocalPlayerEntityId();

	int? GetLastBoundLocalPlayerEntityId();

	void ResetLocalPlayerEntityId();

	string? GetNicknameWithLocalPlayerFallback(int playerId);

	void SetCurrentTarget(int targetId);

	int GetCurrentTarget();

	void FlushDamageData();

	int PurgeEntityLife(int entityId, long cutoffMs);

	int GetEntityLifeGeneration(int entityId);

	long GetEntityLifeRebaseVersion();

	void StartEntityAppearance(int entityId, long appearMs);

	long GetEntityAppearanceStartMs(int entityId);

	void SetMobName(int targetId, string name);

	bool RemoveMobName(int targetId);

	string? GetMobName(int targetId);

	void SetEntityZKey(int entityId, int zKey);

	int GetEntityZKey(int entityId);

	void SetEntityNpcId(int entityId, int npcId);

	int GetEntityNpcId(int entityId);

	void SetPlayerStats(int entityId, int gearScore, int combatPower, int level);

	(int GearScore, int CombatPower, int Level) GetPlayerStats(int entityId);

	void MarkCombatPowerObserved(int entityId, long observedAtMs, CombatPowerSource source, string? name = null)
	{
	}

	CombatPowerObservation? GetCombatPowerObservation(int entityId)
	{
		return null;
	}

	void SetJobClass(int entityId, JobClass jobClass);

	JobClass GetJobClass(int entityId);

	void SetLegion(int entityId, string legion);

	string? GetLegion(int entityId);

	void FlushAllData();

	int GetTotalEventCount();

	long GetEventVersion();
}
