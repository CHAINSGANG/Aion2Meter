using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AionFlex.Core.Interfaces;
using AionFlex.Core.Models;
using AionFlex.Core.Parsing;
using Microsoft.Extensions.Logging;

namespace AionFlex.Infrastructure.PacketParsing;

public sealed class PacketParserFacade : IPacketParser, IDisposable
{
	private readonly ILogger<PacketParserFacade> _logger;

	private readonly IDpsPacketParser _dps;

	private readonly IPlayerStatsPacketParser _stats;

	private readonly IPetPacketParser _pet;

	private readonly IGenusPacketParser _genus;

	private readonly IWingPacketParser _wing;

	private readonly IGearPacketParser _gear;

	private readonly IDaevanionPacketParser _daevanion;

	private readonly ITitlesPacketParser _titles;

	private readonly Channel<CapturedPayload> _featuresChannel;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	private readonly Task _featuresWorker;

	private readonly FeatureStreamReassembler _featureReassembler = new FeatureStreamReassembler();

	public event EventHandler<DamageEvent>? DamageEventParsed;

	public event EventHandler<DamageEvent>? HealTickParsed;

	public event EventHandler<DefenseEvent>? DefenseEventParsed;

	public event EventHandler<AbnormalEvent>? AbnormalEventParsed;

	public event EventHandler<HpUpdateEvent>? HpUpdateEventParsed;

	public event EventHandler<GroggyGuardEvent>? GroggyGuardEventParsed;

	public event EventHandler<BossSkillCastEvent>? BossSkillCastEventParsed;

	public event EventHandler<BossEnrageEvent>? BossEnrageDetected;

	public event EventHandler<PartyRoomRosterEvent>? PartyRoomRosterParsed;

	public event EventHandler<(int PlayerId, string Nickname, int ServerId, bool IsConfirmedPlayer)>? NicknameDiscovered;

	public event EventHandler<(int PlayerId, int AbyssGrade)>? AbyssGradeDiscovered;

	public event EventHandler<AbyssSeasonRecord>? AbyssSeasonRecordDiscovered;

	public event EventHandler<(int EntityId, string Name)>? LocalPlayerDiscovered;

	public event EventHandler<int>? LocalServerIdResolved;

	public event EventHandler<PlayerStatsSnapshot>? PlayerStatsDiscovered;

	public event EventHandler<(int SummonerId, int SummonId)>? SummonDetected;

	public event EventHandler<(int PlayerEntityId, int TargetEntityId)>? PlayerTargetChanged;

	public event EventHandler<(int TargetId, string Name)>? MobNameDiscovered;

	public event EventHandler<(int EntityId, int TemplateCode)>? NpcTemplateDiscovered;

	public event EventHandler<(int EntityId, int NpcId, long TimestampMs, bool FromDispatch)>? NpcAppeared;

	public event EventHandler<(int EntityId, int OwnerId, int NpcId, long TimestampMs)>? SummonAppeared;

	public event EventHandler<(int EntityId, int Reason, long TimestampMs)>? EntityRemoved;

	public event EventHandler<int>? ZoneChanged;

	public event EventHandler<ServerTransferEvent>? ServerTransferred;

	public event EventHandler<DungeonRunStateEvent>? DungeonRunStateChanged;

	public event EventHandler<DungeonRunResultEvent>? DungeonRunResultReceived;

	public event EventHandler<LootEnergyEvent>? LootEnergyUpdated;

	public event EventHandler<ContentsTicketsEvent>? ContentsTicketsUpdated;

	public event EventHandler<CharacterRosterEvent>? CharacterRosterReceived;

	public event EventHandler<WalletEvent>? WalletUpdated;

	public event EventHandler<FieldBossListEvent>? FieldBossListParsed;

	public event EventHandler<(int BaseSkillCode, HashSet<int> SpecIndices, int SkillLevel)>? SkillSpecsDetected;

	public event EventHandler<SkillLoadoutSnapshot>? SkillLoadoutDecoded;

	public event EventHandler<string>? GameServerDiscovered;

	public event EventHandler<long>? LatencyMeasured;

	public event EventHandler<IReadOnlyList<(int PetId, int Level)>>? PetRosterDiscovered;

	public event EventHandler<(uint CharacterId, uint AccountDiscriminator)>? CharacterIdentityDiscovered;

	public event EventHandler<GenusInsightSnapshot>? GenusInsightDiscovered;

	public event EventHandler<EquippedTitlesSnapshot>? EquippedTitlesDiscovered;

	public event EventHandler<DaevanionSnapshot>? DaevanionDiscovered;

	public event EventHandler<WingSnapshot>? WingRosterDiscovered;

	public event EventHandler<(string CharacterName, int WingId)>? EquippedWingForCharacterDiscovered;

	public event EventHandler<int>? EquippedWingForLocalPlayerDiscovered;

	public event EventHandler<IReadOnlyList<GearLoginBundleParser.BundleItem>>? LoginGearBundleDecoded;

	public event EventHandler<(int ItemId, IReadOnlyList<GearSubStat> SubStats)>? GearDetailDecoded;

	public event EventHandler<GearChange>? GearChangeDetected;

	public event EventHandler<(string CharacterName, IReadOnlyList<int> ItemIds)>? EquippedGearForCharacterDiscovered;

	public event EventHandler<IReadOnlyList<int>>? LobbyGearListDiscovered;

	public PacketParserFacade(ILogger<PacketParserFacade> logger, IDpsPacketParser dps, IPlayerStatsPacketParser stats, IPetPacketParser pet, IGenusPacketParser genus, IWingPacketParser wing, IGearPacketParser gear, IDaevanionPacketParser daevanion, ITitlesPacketParser titles)
	{
		_logger = logger;
		_dps = dps;
		_stats = stats;
		_pet = pet;
		_genus = genus;
		_wing = wing;
		_gear = gear;
		_daevanion = daevanion;
		_titles = titles;
		_dps.DamageEventParsed += (object? s, DamageEvent e) =>
		{
			DamageEventParsed?.Invoke(s, e);
		};
		_dps.HealTickParsed += (object? s, DamageEvent e) =>
		{
			HealTickParsed?.Invoke(s, e);
		};
		_dps.DefenseEventParsed += (object? s, DefenseEvent e) =>
		{
			DefenseEventParsed?.Invoke(s, e);
		};
		_dps.AbnormalEventParsed += (object? s, AbnormalEvent e) =>
		{
			AbnormalEventParsed?.Invoke(s, e);
		};
		_dps.HpUpdateEventParsed += (object? s, HpUpdateEvent e) =>
		{
			HpUpdateEventParsed?.Invoke(s, e);
		};
		_dps.GroggyGuardEventParsed += (object? s, GroggyGuardEvent e) =>
		{
			GroggyGuardEventParsed?.Invoke(s, e);
		};
		_dps.BossSkillCastEventParsed += (object? s, BossSkillCastEvent e) =>
		{
			BossSkillCastEventParsed?.Invoke(s, e);
		};
		_dps.BossEnrageDetected += (object? s, BossEnrageEvent e) =>
		{
			BossEnrageDetected?.Invoke(s, e);
		};
		_dps.PartyRoomRosterParsed += (object? s, PartyRoomRosterEvent e) =>
		{
			PartyRoomRosterParsed?.Invoke(s, e);
		};
		_dps.NicknameDiscovered += (object? s, (int PlayerId, string Nickname, int ServerId, bool IsConfirmedPlayer) e) =>
		{
			NicknameDiscovered?.Invoke(s, e);
		};
		_dps.AbyssGradeDiscovered += (object? s, (int PlayerId, int AbyssGrade) e) =>
		{
			AbyssGradeDiscovered?.Invoke(s, e);
		};
		_dps.AbyssSeasonRecordDiscovered += (object? s, AbyssSeasonRecord e) =>
		{
			AbyssSeasonRecordDiscovered?.Invoke(s, e);
		};
		_dps.LocalPlayerDiscovered += (object? s, (int EntityId, string Name) e) =>
		{
			LocalPlayerDiscovered?.Invoke(s, e);
		};
		_dps.LocalServerIdResolved += (object? s, int e) =>
		{
			LocalServerIdResolved?.Invoke(s, e);
		};
		_dps.SummonDetected += (object? s, (int SummonerId, int SummonId) e) =>
		{
			SummonDetected?.Invoke(s, e);
		};
		_dps.PlayerTargetChanged += (object? s, (int PlayerEntityId, int TargetEntityId) e) =>
		{
			PlayerTargetChanged?.Invoke(s, e);
		};
		_dps.MobNameDiscovered += (object? s, (int TargetId, string Name) e) =>
		{
			MobNameDiscovered?.Invoke(s, e);
		};
		_dps.NpcTemplateDiscovered += (object? s, (int EntityId, int TemplateCode) e) =>
		{
			NpcTemplateDiscovered?.Invoke(s, e);
		};
		_dps.NpcAppeared += (object? s, (int EntityId, int NpcId, long TimestampMs, bool FromDispatch) e) =>
		{
			NpcAppeared?.Invoke(s, e);
		};
		_dps.SummonAppeared += (object? s, (int EntityId, int OwnerId, int NpcId, long TimestampMs) e) =>
		{
			SummonAppeared?.Invoke(s, e);
		};
		_dps.EntityRemoved += (object? s, (int EntityId, int Reason, long TimestampMs) e) =>
		{
			EntityRemoved?.Invoke(s, e);
		};
		_dps.ZoneChanged += (object? s, int e) =>
		{
			ZoneChanged?.Invoke(s, e);
		};
		_dps.ServerTransferred += (object? s, ServerTransferEvent e) =>
		{
			ServerTransferred?.Invoke(s, e);
		};
		_dps.DungeonRunStateChanged += (object? s, DungeonRunStateEvent e) =>
		{
			DungeonRunStateChanged?.Invoke(s, e);
		};
		_dps.DungeonRunResultReceived += (object? s, DungeonRunResultEvent e) =>
		{
			DungeonRunResultReceived?.Invoke(s, e);
		};
		_dps.LootEnergyUpdated += (object? s, LootEnergyEvent e) =>
		{
			LootEnergyUpdated?.Invoke(s, e);
		};
		_dps.ContentsTicketsUpdated += (object? s, ContentsTicketsEvent e) =>
		{
			ContentsTicketsUpdated?.Invoke(s, e);
		};
		_dps.CharacterRosterReceived += (object? s, CharacterRosterEvent e) =>
		{
			CharacterRosterReceived?.Invoke(s, e);
		};
		_dps.WalletUpdated += (object? s, WalletEvent e) =>
		{
			WalletUpdated?.Invoke(s, e);
		};
		_dps.FieldBossListParsed += (object? s, FieldBossListEvent e) =>
		{
			FieldBossListParsed?.Invoke(s, e);
		};
		_dps.SkillSpecsDetected += (object? s, (int BaseSkillCode, HashSet<int> SpecIndices, int SkillLevel) e) =>
		{
			SkillSpecsDetected?.Invoke(s, e);
		};
		_dps.SkillLoadoutDecoded += (object? s, SkillLoadoutSnapshot e) =>
		{
			SkillLoadoutDecoded?.Invoke(s, e);
		};
		_dps.GameServerDiscovered += (object? s, string e) =>
		{
			GameServerDiscovered?.Invoke(s, e);
		};
		_dps.LatencyMeasured += (object? s, long e) =>
		{
			LatencyMeasured?.Invoke(s, e);
		};
		_stats.PlayerStatsDiscovered += (object? s, PlayerStatsSnapshot e) =>
		{
			PlayerStatsDiscovered?.Invoke(s, e);
		};
		_pet.PetRosterDiscovered += (object? s, IReadOnlyList<(int PetId, int Level)> e) =>
		{
			PetRosterDiscovered?.Invoke(s, e);
		};
		_genus.GenusInsightDiscovered += (object? s, GenusInsightSnapshot e) =>
		{
			GenusInsightDiscovered?.Invoke(s, e);
		};
		_wing.WingRosterDiscovered += (object? s, WingSnapshot e) =>
		{
			WingRosterDiscovered?.Invoke(s, e);
		};
		_wing.EquippedWingForCharacterDiscovered += (object? s, (string CharacterName, int WingId) e) =>
		{
			EquippedWingForCharacterDiscovered?.Invoke(s, e);
		};
		_wing.EquippedWingForLocalPlayerDiscovered += (object? s, int e) =>
		{
			EquippedWingForLocalPlayerDiscovered?.Invoke(s, e);
		};
		_gear.LoginGearBundleDecoded += (object? s, IReadOnlyList<GearLoginBundleParser.BundleItem> e) =>
		{
			LoginGearBundleDecoded?.Invoke(s, e);
		};
		_gear.GearDetailDecoded += (object? s, (int ItemId, IReadOnlyList<GearSubStat> SubStats) e) =>
		{
			GearDetailDecoded?.Invoke(s, e);
		};
		_gear.GearChangeDetected += (object? s, GearChange e) =>
		{
			GearChangeDetected?.Invoke(s, e);
		};
		_gear.EquippedGearForCharacterDiscovered += (object? s, (string CharacterName, IReadOnlyList<int> ItemIds) e) =>
		{
			EquippedGearForCharacterDiscovered?.Invoke(s, e);
		};
		_gear.LobbyGearListDiscovered += (object? s, IReadOnlyList<int> e) =>
		{
			LobbyGearListDiscovered?.Invoke(s, e);
		};
		_daevanion.DaevanionDiscovered += (object? s, DaevanionSnapshot e) =>
		{
			DaevanionDiscovered?.Invoke(s, e);
		};
		_titles.EquippedTitlesDiscovered += (object? s, EquippedTitlesSnapshot e) =>
		{
			EquippedTitlesDiscovered?.Invoke(s, e);
		};
		_genus.CharacterIdentityDiscovered += (object? s, (uint CharacterId, uint AccountDiscriminator) e) =>
		{
			CharacterIdentityDiscovered?.Invoke(s, e);
		};
		_featuresChannel = Channel.CreateUnbounded<CapturedPayload>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = false
		});
		_featuresWorker = Task.Run((Func<Task?>)FeaturesWorkerLoop);
		_logger.LogInformation("PacketParserFacade ready — DPS on capture thread, features on background thread");
	}

	public void SetValidWingIds(IReadOnlySet<int> wingIds)
	{
		_wing.SetValidWingIds(wingIds);
	}

	public void SetValidItemIds(IReadOnlySet<int> itemIds)
	{
		_gear.SetValidItemIds(itemIds);
	}

	public void SetValidTitleIds(IReadOnlySet<int> titleIds)
	{
		_titles.SetValidTitleIds(titleIds);
	}

	public void SetCurrentTarget(int targetId)
	{
		_dps.SetCurrentTarget(targetId);
	}

	public void ResetEntityLife(int entityId)
	{
		_dps.ResetEntityLife(entityId);
	}

	public void ClearDiscoveredEntities()
	{
		_dps.ClearDiscoveredEntities();
		_featureReassembler.Reset();
	}

	public void ProcessPayload(CapturedPayload payload)
	{
		try
		{
			_dps.ProcessPayload(payload);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "DpsPacketParser threw while processing payload");
		}
		if (!_featuresChannel.Writer.TryWrite(payload))
		{
			_logger.LogWarning("PacketParserFacade: features channel rejected payload (channel closed?)");
		}
	}

	private async Task FeaturesWorkerLoop()
	{
		ChannelReader<CapturedPayload> reader = _featuresChannel.Reader;
		try
		{
			await foreach (CapturedPayload item in reader.ReadAllAsync(_cts.Token).ConfigureAwait(continueOnCapturedContext: false))
			{
				SafeFeatureCall(_stats, item, "PlayerStats");
				SafeFeatureCall(_pet, item, "Pet");
				CapturedPayload payload = _featureReassembler.Accumulate(item);
				SafeFeatureCall(_genus, payload, "Genus");
				SafeFeatureCall(_wing, payload, "Wing");
				SafeFeatureCall(_gear, payload, "Gear");
				SafeFeatureCall(_daevanion, payload, "Daevanion");
				SafeFeatureCall(_titles, payload, "Titles");
				try
				{
					_dps.ProcessReassembledStreamForSeasonRecord(payload);
				}
				catch (Exception exception)
				{
					_logger.LogError(exception, "Abyss season scan threw while processing payload (isolated, other parsers continue)");
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception2)
		{
			_logger.LogError(exception2, "PacketParserFacade features worker stopped unexpectedly");
		}
	}

	private void SafeFeatureCall(IFeaturePacketParser parser, CapturedPayload payload, string name)
	{
		try
		{
			parser.ProcessPayload(payload);
		}
		catch (Exception exception)
		{
			_logger.LogError(exception, "{Parser}PacketParser threw while processing payload (isolated, other parsers continue)", name);
		}
	}

	public void Dispose()
	{
		try
		{
			_featuresChannel.Writer.TryComplete();
			_cts.Cancel();
			_featuresWorker.Wait(TimeSpan.FromSeconds(2.0));
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "PacketParserFacade dispose encountered errors");
		}
		finally
		{
			_cts.Dispose();
		}
	}
}
