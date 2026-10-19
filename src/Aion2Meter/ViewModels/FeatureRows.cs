namespace Aion2Meter.ViewModels;

public sealed record PartyMemberRowViewModel(
    int Slot,
    string Name,
    string ClassName,
    int Level,
    int GearScore,
    int CombatPower,
    string RoleMark,
    string LocationText);
