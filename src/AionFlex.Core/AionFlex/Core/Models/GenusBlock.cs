using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record GenusBlock(GenusType Type, int Level, IReadOnlyList<GenusEffect> Effects);
