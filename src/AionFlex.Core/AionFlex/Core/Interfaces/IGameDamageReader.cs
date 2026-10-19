using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IGameDamageReader
{
	int GearScore { get; }

	bool IsRunning { get; }

	bool IsConnected { get; }

	string? PlayerName { get; }

	int PlayerEntityId { get; }

	int TotalHitsRead { get; }

	float GameDps { get; }

	int GameTotalDamage { get; }

	IReadOnlySet<int> ActiveBossEntityIds { get; }

	event EventHandler<DamageEvent>? DamageEventRead;

	event EventHandler<(int EntityId, string PlayerName)>? PlayerDiscovered;

	event EventHandler<(int EntityId, string Name, bool IsBoss, long HpOrDamage)>? TargetDiscovered;

	event EventHandler<(int EntityId, string BossName)>? BossReset;

	event EventHandler<(int EntityId, string BossName, long MaxHp)>? BossEngaged;

	event EventHandler<HpUpdateEvent>? HpUpdateEventRead;

	void Start();

	void Stop();
}
