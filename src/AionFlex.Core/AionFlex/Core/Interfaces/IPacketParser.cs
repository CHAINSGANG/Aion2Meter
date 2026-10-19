using System;
using System.Collections.Generic;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;

namespace AionFlex.Core.Interfaces;

public interface IPacketParser
{
	event EventHandler<DamageEvent>? DamageEventParsed;

	event EventHandler<DamageEvent>? HealTickParsed;

	event EventHandler<DefenseEvent>? DefenseEventParsed;

	event EventHandler<AbnormalEvent>? AbnormalEventParsed;

	event EventHandler<HpUpdateEvent>? HpUpdateEventParsed;

	event EventHandler<GroggyGuardEvent>? GroggyGuardEventParsed;

	event EventHandler<BossSkillCastEvent>? BossSkillCastEventParsed;

	event EventHandler<BossEnrageEvent>? BossEnrageDetected;

	event EventHandler<PartyRoomRosterEvent>? PartyRoomRosterParsed;

	event EventHandler<(int PlayerId, string Nickname, int ServerId, bool IsConfirmedPlayer)>? NicknameDiscovered;

	event EventHandler<(int PlayerId, int AbyssGrade)>? AbyssGradeDiscovered;

	event EventHandler<AbyssSeasonRecord>? AbyssSeasonRecordDiscovered;

	event EventHandler<(int EntityId, string Name)>? LocalPlayerDiscovered;

	event EventHandler<int>? LocalServerIdResolved;

	event EventHandler<PlayerStatsSnapshot>? PlayerStatsDiscovered;

	event EventHandler<(int SummonerId, int SummonId)>? SummonDetected;

	event EventHandler<(int PlayerEntityId, int TargetEntityId)>? PlayerTargetChanged;

	event EventHandler<(int TargetId, string Name)>? MobNameDiscovered;

	event EventHandler<(int EntityId, int TemplateCode)>? NpcTemplateDiscovered;

	event EventHandler<(int EntityId, int NpcId, long TimestampMs, bool FromDispatch)>? NpcAppeared;

	event EventHandler<(int EntityId, int OwnerId, int NpcId, long TimestampMs)>? SummonAppeared;

	event EventHandler<(int EntityId, int Reason, long TimestampMs)>? EntityRemoved;

	event EventHandler<int>? ZoneChanged;

	event EventHandler<ServerTransferEvent>? ServerTransferred;

	event EventHandler<DungeonRunStateEvent>? DungeonRunStateChanged;

	event EventHandler<DungeonRunResultEvent>? DungeonRunResultReceived;

	event EventHandler<LootEnergyEvent>? LootEnergyUpdated;

	event EventHandler<ContentsTicketsEvent>? ContentsTicketsUpdated;

	event EventHandler<CharacterRosterEvent>? CharacterRosterReceived;

	event EventHandler<WalletEvent>? WalletUpdated;

	event EventHandler<FieldBossListEvent>? FieldBossListParsed;

	event EventHandler<(int BaseSkillCode, HashSet<int> SpecIndices, int SkillLevel)>? SkillSpecsDetected;

	event EventHandler<SkillLoadoutSnapshot>? SkillLoadoutDecoded;

	event EventHandler<string>? GameServerDiscovered;

	event EventHandler<long>? LatencyMeasured;

	event EventHandler<IReadOnlyList<(int PetId, int Level)>>? PetRosterDiscovered;

	event EventHandler<(uint CharacterId, uint AccountDiscriminator)>? CharacterIdentityDiscovered;

	event EventHandler<GenusInsightSnapshot>? GenusInsightDiscovered;

	event EventHandler<EquippedTitlesSnapshot>? EquippedTitlesDiscovered;

	event EventHandler<DaevanionSnapshot>? DaevanionDiscovered;

	event EventHandler<WingSnapshot>? WingRosterDiscovered;

	event EventHandler<(string CharacterName, int WingId)>? EquippedWingForCharacterDiscovered;

	event EventHandler<int>? EquippedWingForLocalPlayerDiscovered;

	event EventHandler<IReadOnlyList<GearLoginBundleParser.BundleItem>>? LoginGearBundleDecoded;

	event EventHandler<(int ItemId, IReadOnlyList<GearSubStat> SubStats)>? GearDetailDecoded;

	event EventHandler<GearChange>? GearChangeDetected;

	event EventHandler<(string CharacterName, IReadOnlyList<int> ItemIds)>? EquippedGearForCharacterDiscovered;

	event EventHandler<IReadOnlyList<int>>? LobbyGearListDiscovered;

	void SetValidWingIds(IReadOnlySet<int> wingIds);

	void SetValidTitleIds(IReadOnlySet<int> titleIds);

	void SetValidItemIds(IReadOnlySet<int> itemIds);

	void ProcessPayload(CapturedPayload payload);

	void SetCurrentTarget(int targetId);

	void ClearDiscoveredEntities();

	void ResetEntityLife(int entityId);
}
