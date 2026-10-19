using System.Collections.Generic;

namespace AionFlex.Core.Armory;

public sealed record ArmoryEquipment(IReadOnlyList<ArmoryItem> Items, ArmoryCompanion? Pet, ArmoryCompanion? Wing);
