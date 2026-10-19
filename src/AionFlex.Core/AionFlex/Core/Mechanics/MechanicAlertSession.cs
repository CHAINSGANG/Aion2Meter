using System;
using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Mechanics;

public sealed class MechanicAlertSession
{
	private readonly object _gate = new object();

	private readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);

	private int _fightEntityId;

	private string? _entryId;

	private int _epoch;

	public int Epoch
	{
		get
		{
			lock (_gate)
			{
				return _epoch;
			}
		}
	}

	public void OnWipeReset(int entityId)
	{
		ResetIfFight(entityId);
	}

	public void OnEntityNewLife(int entityId)
	{
		ResetIfFight(entityId);
	}

	private void ResetIfFight(int entityId)
	{
		lock (_gate)
		{
			if (entityId > 0 && entityId == _fightEntityId)
			{
				_fired.Clear();
				_epoch++;
			}
		}
	}

	public IReadOnlyList<MechanicAlertPolicy.Alert> Evaluate(BossMechanics? entry, int fightEntityId, int observedEpoch, MechanicAlertPolicy.Signals signals)
	{
		lock (_gate)
		{
			if (entry == null || fightEntityId <= 0)
			{
				return Array.Empty<MechanicAlertPolicy.Alert>();
			}
			if (observedEpoch != _epoch)
			{
				return Array.Empty<MechanicAlertPolicy.Alert>();
			}
			if (fightEntityId != _fightEntityId || !string.Equals(entry.Id, _entryId, StringComparison.Ordinal))
			{
				_fightEntityId = fightEntityId;
				_entryId = entry.Id;
				_fired.Clear();
			}
			return MechanicAlertPolicy.Evaluate(entry.Mechanics, _fired, signals);
		}
	}
}
