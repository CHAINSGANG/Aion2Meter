using System;

namespace AionFlex.Core.Models;

[Flags]
public enum SpecialDamageFlags
{
	None = 0,
	Back = 1,
	Parry = 2,
	Block = 4,
	Perfect = 8,
	Double = 0x10,
	Endure = 0x20,
	Unknown4 = 0x40,
	PowerShard = 0x80
}
