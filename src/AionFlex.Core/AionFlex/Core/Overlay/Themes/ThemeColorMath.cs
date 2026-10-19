using System;

namespace AionFlex.Core.Overlay.Themes;

public static class ThemeColorMath
{
	public static uint WithAlpha(uint argb, byte alpha)
	{
		return (argb & 0xFFFFFF) | (uint)(alpha << 24);
	}

	public static uint WithAlpha(uint argb, double alpha01)
	{
		double num = (double.IsNaN(alpha01) ? 0.0 : Math.Clamp(alpha01, 0.0, 1.0));
		return WithAlpha(argb, (byte)Math.Round(num * 255.0, MidpointRounding.AwayFromZero));
	}

	public static uint MixWhite(uint argb, double colorWeight)
	{
		return Mix(argb, colorWeight, 255);
	}

	public static uint MixBlack(uint argb, double colorWeight)
	{
		return Mix(argb, colorWeight, 0);
	}

	public static uint Clear(uint argb)
	{
		return argb & 0xFFFFFF;
	}

	private static uint Mix(uint argb, double colorWeight, int other)
	{
		double p = (double.IsNaN(colorWeight) ? 0.0 : Math.Clamp(colorWeight, 0.0, 1.0));
		return 0xFF000000u | Channel(16) | Channel(8) | Channel(0);
		uint Channel(int shift)
		{
			int num = (int)((argb >> shift) & 0xFF);
			return (uint)Math.Round(p * (double)num + (1.0 - p) * (double)other, MidpointRounding.AwayFromZero) << shift;
		}
	}
}
