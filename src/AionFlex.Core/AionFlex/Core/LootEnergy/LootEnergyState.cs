using AionFlex.Core.Parsing;

namespace AionFlex.Core.LootEnergy;

public sealed class LootEnergyState
{
	private string? _boundCharacter;

	private bool _listSinceBind;

	public LootEnergyReading? Current { get; private set; }

	public bool OnLoginList(LootEnergyReading reading)
	{
		_listSinceBind = true;
		return Set(reading);
	}

	public bool OnLiveChange(LootEnergyReading reading)
	{
		return Set(reading);
	}

	public bool OnCharacterBound(string characterKey)
	{
		bool result = false;
		if (_boundCharacter != null && _boundCharacter != characterKey && !_listSinceBind && Current.HasValue)
		{
			Current = null;
			result = true;
		}
		_boundCharacter = characterKey;
		_listSinceBind = false;
		return result;
	}

	private bool Set(LootEnergyReading reading)
	{
		if (Current == reading)
		{
			return false;
		}
		Current = reading;
		return true;
	}
}
