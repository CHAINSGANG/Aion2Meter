using System;
using System.Collections.Generic;
using AionFlex.Core.Combat;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IDpsCalculator
{
	bool IsBossOnlyMode { get; }

	DpsSnapshot CalculateDps();

	DpsSnapshot CalculateDpsForTarget(int targetId);

	DpsSnapshot CalculateDamageTaken();

	DpsSnapshot CalculateHealing(HealingFightWindow window);

	void Reset();

	void SetBossOnlyMode(bool bossOnly);

	void SetMaxHpProvider(Func<int, uint>? provider);

	void SetTrustedMaxHpProvider(Func<int, long, long?>? provider);

	void SetHpProgressProvider(Func<int, HpProgress?>? provider);

	int GetCurrentTargetId();

	string GetCurrentTargetDisplayName();

	long GetTargetFirstDamageTime(int targetId);

	long GetTargetLastDamageTime(int targetId);

	IReadOnlyList<int> GetKnownTargetIds();

	IReadOnlyDictionary<int, DpsSnapshot> GetPerTargetSnapshots();
}
