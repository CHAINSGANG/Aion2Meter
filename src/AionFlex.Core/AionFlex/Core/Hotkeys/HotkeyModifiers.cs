using System;

namespace AionFlex.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
	None = 0,
	Alt = 1,
	Control = 2,
	Shift = 4,
	Win = 8
}
