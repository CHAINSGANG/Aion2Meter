namespace AionFlex.Core.Overlay;

public readonly record struct WindowAboveUs(uint ProcessId, bool IsVisible, bool IsCloaked, bool IsTopmost, ScreenRect Bounds);
