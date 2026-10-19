using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public sealed class MechanicsSheet
{
	public required string Id { get; init; }

	public required LocalizedText Title { get; init; }

	public double CanvasWidth { get; init; } = 1000.0;

	public double CanvasHeight { get; init; } = 620.0;

	public bool CircularArena { get; init; }

	public SheetPoint? Boss { get; init; }

	public IReadOnlyList<SheetZoneLabel> Zones { get; init; } = Array.Empty<SheetZoneLabel>();

	public IReadOnlyList<SheetHazard> Hazards { get; init; } = Array.Empty<SheetHazard>();

	public IReadOnlyList<SheetPlayerSpot> PlayerSpots { get; init; } = Array.Empty<SheetPlayerSpot>();

	public IReadOnlyList<SheetPoint> RollPath { get; init; } = Array.Empty<SheetPoint>();

	public IReadOnlyList<SheetOrb> Orbs { get; init; } = Array.Empty<SheetOrb>();

	public IReadOnlyList<SheetPoint> Voids { get; init; } = Array.Empty<SheetPoint>();

	public IReadOnlyList<SheetPoint> Portals { get; init; } = Array.Empty<SheetPoint>();

	public IReadOnlyList<SheetPath> Paths { get; init; } = Array.Empty<SheetPath>();

	public IReadOnlyList<SheetCover> Covers { get; init; } = Array.Empty<SheetCover>();
}
