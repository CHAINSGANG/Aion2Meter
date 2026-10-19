using System;

namespace AionFlex.Core.Overlay.Themes;

public sealed class DpsHistoryRing
{
	public const int Capacity = 40;

	private readonly double[] _samples = new double[40];

	private int _next;

	public int Count { get; private set; }

	public int Version { get; private set; }

	public void Push(double dps)
	{
		_samples[_next] = ((double.IsFinite(dps) && dps > 0.0) ? dps : 0.0);
		_next = (_next + 1) % 40;
		if (Count < 40)
		{
			Count++;
		}
		Version++;
	}

	public int CopyTo(Span<double> destination)
	{
		int num = Math.Min(Count, destination.Length);
		int num2 = (_next - num + 40) % 40;
		for (int i = 0; i < num; i++)
		{
			destination[i] = _samples[(num2 + i) % 40];
		}
		return num;
	}

	public double Max()
	{
		double num = 0.0;
		int num2 = (_next - Count + 40) % 40;
		for (int i = 0; i < Count; i++)
		{
			num = Math.Max(num, _samples[(num2 + i) % 40]);
		}
		return num;
	}
}
