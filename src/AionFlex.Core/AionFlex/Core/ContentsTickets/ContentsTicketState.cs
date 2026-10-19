using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.ContentsTickets;

public sealed class ContentsTicketState
{
	private readonly Dictionary<string, CharacterCounters> _byCharacter = new Dictionary<string, CharacterCounters>(StringComparer.Ordinal);

	private Dictionary<uint, ContentsTicketEntry>? _heldList;

	private long _heldAtMs;

	private string? _bound;

	public CharacterCounters? Current
	{
		get
		{
			if (_bound == null || !_byCharacter.TryGetValue(_bound, out CharacterCounters value))
			{
				return null;
			}
			return value;
		}
	}

	public CharacterCounters? Of(string character)
	{
		if (!_byCharacter.TryGetValue(character, out CharacterCounters value))
		{
			return null;
		}
		return value;
	}

	public bool OnList(IReadOnlyList<ContentsTicketEntry> entries, long atMs)
	{
		_heldList = new Dictionary<uint, ContentsTicketEntry>();
		foreach (ContentsTicketEntry entry in entries)
		{
			_heldList[entry.TicketId] = entry;
		}
		_heldAtMs = atMs;
		return false;
	}

	public bool OnChange(ContentsTicketEntry entry, long atMs)
	{
		if (_heldList != null)
		{
			_heldList[entry.TicketId] = entry;
			return false;
		}
		if (_bound == null || !_byCharacter.TryGetValue(_bound, out CharacterCounters value))
		{
			return false;
		}
		if (value.Entries.TryGetValue(entry.TicketId, out var value2) && value2 == entry)
		{
			return false;
		}
		Dictionary<uint, ContentsTicketEntry> entries = new Dictionary<uint, ContentsTicketEntry>(value.Entries) { [entry.TicketId] = entry };
		_byCharacter[_bound] = value with
		{
			Entries = entries,
			ReadAtMs = atMs
		};
		return true;
	}

	public bool OnCharacterBound(string character, string? region = null, int? serverId = null)
	{
		bool result = character != _bound;
		_bound = character;
		if (_heldList == null)
		{
			return result;
		}
		_byCharacter[character] = new CharacterCounters(character, _heldList, _heldAtMs, region, serverId);
		_heldList = null;
		return true;
	}
}
