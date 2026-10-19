using System;

namespace AionFlex.Core.Models;

public sealed class ValueDelta
{
	public double Baseline { get; init; }

	public double Compared { get; init; }

	public double AbsoluteDelta => Compared - Baseline;

	public double PercentDelta
	{
		get
		{
			if (Baseline == 0.0)
			{
				return (Compared != 0.0) ? 100 : 0;
			}
			return (Compared - Baseline) / Baseline * 100.0;
		}
	}

	public bool IsImprovement => AbsoluteDelta > 0.0;

	public bool IsRegression => AbsoluteDelta < 0.0;

	public bool IsNeutral => Math.Abs(AbsoluteDelta) < 0.001;

	public static ValueDelta Create(double baseline, double compared)
	{
		return new ValueDelta
		{
			Baseline = baseline,
			Compared = compared
		};
	}
}
