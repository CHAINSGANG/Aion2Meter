namespace AionFlex.Core.Models;

public readonly record struct CombatPowerObservation(long ObservedAtMs, CombatPowerSource Source, string? Name = null);
