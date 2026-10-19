using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed record PartyRoomRoster(uint RoomId, string Title, int MaxMembers, int DungeonId, uint LeaderCharacterId, int LeaderServerId, IReadOnlyList<PartyRoomMember> Members)
{
	public bool IsLeader(PartyRoomMember member)
	{
		if (member.CharacterId == LeaderCharacterId)
		{
			return member.ServerId == LeaderServerId;
		}
		return false;
	}

	public PartyRoomMember? FindCharacter(string? name, int? serverId)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		foreach (PartyRoomMember member in Members)
		{
			if (!(member.Name == name))
			{
				continue;
			}
			if (serverId.HasValue)
			{
				int valueOrDefault = serverId.GetValueOrDefault();
				if (member.ServerId != valueOrDefault && member.WorldServerId != valueOrDefault)
				{
					continue;
				}
			}
			return member;
		}
		return null;
	}

	public bool HasSameContent(PartyRoomRoster other)
	{
		if ((object)other == null)
		{
			return false;
		}
		if (RoomId != other.RoomId || Title != other.Title || MaxMembers != other.MaxMembers || DungeonId != other.DungeonId || LeaderCharacterId != other.LeaderCharacterId || LeaderServerId != other.LeaderServerId || Members.Count != other.Members.Count)
		{
			return false;
		}
		for (int i = 0; i < Members.Count; i++)
		{
			if (Members[i] != other.Members[i])
			{
				return false;
			}
		}
		return true;
	}
}
