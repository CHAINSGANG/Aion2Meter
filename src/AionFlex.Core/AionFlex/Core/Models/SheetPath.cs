using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class SheetPath
{
	public SheetTeam Team { get; init; }

	public IReadOnlyList<SheetPoint> Points { get; init; } = Array.Empty<SheetPoint>();

	public bool Faint { get; init; }

	public LocalizedText? Label { get; init; }
}
