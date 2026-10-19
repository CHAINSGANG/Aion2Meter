namespace AionFlex.Core.Models;

public sealed record PartyRoomMember(int Slot, uint CharacterId, int ServerId, string Name, int PcId, JobClass JobClass, int Level, int GearScore, int CombatPower, int WorldServerId, int CurrentServerId)
{
	public bool IsInInstance => CurrentServerId != WorldServerId;
}
