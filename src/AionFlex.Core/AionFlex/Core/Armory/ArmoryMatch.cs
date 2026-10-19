using System.Collections.Generic;

namespace AionFlex.Core.Armory;

public sealed record ArmoryMatch(ArmoryCharacter? Picked, IReadOnlyList<ArmoryCharacter> Candidates);
