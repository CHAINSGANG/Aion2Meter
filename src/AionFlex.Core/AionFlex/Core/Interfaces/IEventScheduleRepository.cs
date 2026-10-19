using System.Collections.Generic;
using AionFlex.Core.Models;

namespace AionFlex.Core.Interfaces;

public interface IEventScheduleRepository
{
	IReadOnlyList<GameEvent> GetEvents(string service);
}
