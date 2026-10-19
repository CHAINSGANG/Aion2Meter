using System.Collections.Generic;
using AionFlex.Core.Checklist;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IChecklistService
{
	string? ActiveCharacter { get; }

	ChecklistResetClock ResetClock { get; }

	IReadOnlyList<string> KnownCharacters { get; }

	void SwitchCharacter(string characterName);

	IReadOnlyList<ChecklistItem> GetItems();

	void SetChecked(string itemId, bool isChecked);

	void ResetAll();

	void RemoveCharacter(string characterName);

	ChecklistProgress ProgressOf(string characterName);

	void SetAll(bool weekly, bool isChecked);
}
