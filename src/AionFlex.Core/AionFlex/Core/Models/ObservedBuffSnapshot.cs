using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class ObservedBuffSnapshot
{
	private readonly bool? _streamLive;

	public IReadOnlyDictionary<int, ObservedBuff> ByAbnormalId { get; init; } = new Dictionary<int, ObservedBuff>();

	public bool StreamLive
	{
		get
		{
			return _streamLive ?? (ByAbnormalId.Count > 0);
		}
		init
		{
			_streamLive = value;
		}
	}

	public static ObservedBuffSnapshot Empty { get; } = new ObservedBuffSnapshot();
}
