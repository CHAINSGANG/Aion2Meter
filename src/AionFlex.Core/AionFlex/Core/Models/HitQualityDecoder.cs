using System;
using System.Collections.Generic;

namespace AionFlex.Core.Models;

public static class HitQualityDecoder
{
	public const int MaxCompositeType = 35;

	public const int QualityBlockLength = 11;

	public const int ModifierByteOffset = 0;

	public const int DirectionByteOffset = 2;

	public const byte DoubleBit = 8;

	public const byte PerfectBit = 4;

	private const byte DirectionSideValue = 0;

	private const byte DirectionBackValue = 1;

	private const byte DirectionFrontValue = 2;

	public const int MultiHitSwitchBit = 32;

	public const int MaxChainIndex = 16;

	public static FloaterTypeParts DecodeFloaterType(int type)
	{
		if (type < 0 || type > 35)
		{
			return new FloaterTypeParts(0, Critical: false, Back: false, HitModifier.None);
		}
		int hitIndex = type % 3;
		int num = type / 3;
		return new FloaterTypeParts(hitIndex, (num & 1) != 0, (num & 2) != 0, (num >> 2) switch
		{
			1 => HitModifier.Double, 
			2 => HitModifier.Perfect, 
			_ => HitModifier.None, 
		});
	}

	public static FloaterTypeParts DecodeFloaterType(DamageFloaterType type)
	{
		return DecodeFloaterType((int)type);
	}

	public static (HitDirection Direction, HitModifier Modifier) DecodeWireQuality(ReadOnlySpan<byte> flagsBlock)
	{
		if (flagsBlock.Length != 11)
		{
			return (Direction: HitDirection.Unknown, Modifier: HitModifier.Unknown);
		}
		HitDirection hitDirection = flagsBlock[2] switch
		{
			0 => HitDirection.Side, 
			1 => HitDirection.Back, 
			2 => HitDirection.Front, 
			_ => HitDirection.Unknown, 
		};
		if (hitDirection == HitDirection.Unknown)
		{
			return (Direction: HitDirection.Unknown, Modifier: HitModifier.Unknown);
		}
		byte b = flagsBlock[0];
		HitModifier hitModifier = HitModifier.Unknown;
		if ((b & 8) != 0)
		{
			hitModifier |= HitModifier.Double;
		}
		if ((b & 4) != 0)
		{
			hitModifier |= HitModifier.Perfect;
		}
		if (hitModifier == HitModifier.Unknown)
		{
			hitModifier = HitModifier.None;
		}
		return (Direction: hitDirection, Modifier: hitModifier);
	}

	public static bool DecodeMultiHit(int switchVar)
	{
		return (switchVar & 0x20) != 0;
	}

	public static int DecodeChainIndex(ReadOnlySpan<byte> flagsBlock)
	{
		if (flagsBlock.Length != 11)
		{
			return 0;
		}
		uint num = (uint)(flagsBlock[flagsBlock.Length - 4] | (flagsBlock[flagsBlock.Length - 3] << 8) | (flagsBlock[flagsBlock.Length - 2] << 16) | (flagsBlock[flagsBlock.Length - 1] << 24));
		if (num < 1 || num > 16)
		{
			return 0;
		}
		return (int)num;
	}

	public static HitQuality Decode(int type, ReadOnlySpan<byte> flagsBlock, int? switchVar = null)
	{
		(HitDirection Direction, HitModifier Modifier) tuple = DecodeWireQuality(flagsBlock);
		HitDirection item = tuple.Direction;
		HitModifier item2 = tuple.Modifier;
		bool critical = DecodeFloaterType(type).Critical;
		int chainIndex = DecodeChainIndex(flagsBlock);
		bool? multiHitProc;
		if (switchVar.HasValue)
		{
			int valueOrDefault = switchVar.GetValueOrDefault();
			multiHitProc = DecodeMultiHit(valueOrDefault);
		}
		else
		{
			multiHitProc = null;
		}
		return new HitQuality(critical, item, item2, chainIndex, multiHitProc);
	}

	public static string RenderChatLabel(HitQuality q)
	{
		List<string> list = new List<string>(3);
		if (q.IsBack)
		{
			list.Add("Back");
		}
		else if (q.IsFront)
		{
			list.Add("Front");
		}
		if (q.IsDouble)
		{
			list.Add("Double");
		}
		else if (q.IsPerfect)
		{
			list.Add("Perfect");
		}
		if (q.Critical)
		{
			list.Add("Critical");
		}
		return string.Join(" ", list);
	}
}
