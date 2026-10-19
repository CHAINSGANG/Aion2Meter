using System;
using System.Collections.Generic;

namespace AionFlex.Core.Alerts;

public sealed class AlertSoundTransitionDetector
{
	public const int Memory = 64;

	private readonly Dictionary<AlertSoundKind, (HashSet<string> Set, Queue<string> Order)> _played = new Dictionary<AlertSoundKind, (HashSet<string>, Queue<string>)>();

	public bool Observe(AlertSoundKind kind, string? occurrenceKey)
	{
		if (string.IsNullOrEmpty(occurrenceKey))
		{
			return false;
		}
		if (!_played.TryGetValue(kind, out (HashSet<string>, Queue<string>) value))
		{
			value = (new HashSet<string>(StringComparer.Ordinal), new Queue<string>());
			_played[kind] = value;
		}
		if (!value.Item1.Add(occurrenceKey))
		{
			return false;
		}
		value.Item2.Enqueue(occurrenceKey);
		while (value.Item2.Count > 64)
		{
			value.Item1.Remove(value.Item2.Dequeue());
		}
		return true;
	}
}
