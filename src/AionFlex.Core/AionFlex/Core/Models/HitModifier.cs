using System;

namespace AionFlex.Core.Models;

[Flags]
public enum HitModifier
{
	Unknown = 0,
	None = 1,
	Double = 2,
	Perfect = 4
}
