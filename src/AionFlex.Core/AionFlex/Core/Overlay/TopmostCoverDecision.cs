namespace AionFlex.Core.Overlay;

public readonly record struct TopmostCoverDecision(bool ShouldRaise, int CoverIndex, TopmostCoverReason Reason);
