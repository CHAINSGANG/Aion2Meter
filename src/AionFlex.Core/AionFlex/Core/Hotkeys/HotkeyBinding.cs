namespace AionFlex.Core.Hotkeys;

public readonly record struct HotkeyBinding(bool Enabled, HotkeyModifiers Modifiers, int Key);
