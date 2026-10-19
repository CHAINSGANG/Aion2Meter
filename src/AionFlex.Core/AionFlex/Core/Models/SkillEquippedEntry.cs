using System.Collections.Generic;

namespace AionFlex.Core.Models;

public readonly record struct SkillEquippedEntry(int BaseCode, int Level, IReadOnlyList<int> Specs);
