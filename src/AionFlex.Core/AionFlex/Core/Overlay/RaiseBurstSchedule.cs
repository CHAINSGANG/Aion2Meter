using System;
using System.Collections.Generic;

namespace AionFlex.Core.Overlay;

public sealed class RaiseBurstSchedule
{
	private readonly int[] _deltasMs;

	private readonly int[] _cumulativeMs;

	private int _nextStep;

	public IReadOnlyList<int> DeltasMs => _deltasMs;

	public IReadOnlyList<int> CumulativeOffsetsMs => _cumulativeMs;

	public int RaisesPerTransition => _deltasMs.Length + 1;

	public bool IsRunning => _nextStep < _deltasMs.Length;

	public RaiseBurstSchedule(IReadOnlyList<int> deltasMs)
	{
		ArgumentNullException.ThrowIfNull(deltasMs, "deltasMs");
		if (deltasMs.Count == 0)
		{
			throw new ArgumentException("a burst needs at least one follow-up", "deltasMs");
		}
		_deltasMs = new int[deltasMs.Count];
		_cumulativeMs = new int[deltasMs.Count];
		int num = 0;
		for (int i = 0; i < deltasMs.Count; i++)
		{
			if (deltasMs[i] <= 0)
			{
				throw new ArgumentOutOfRangeException("deltasMs", deltasMs[i], "every delay must be > 0 ms");
			}
			_deltasMs[i] = deltasMs[i];
			num += deltasMs[i];
			_cumulativeMs[i] = num;
		}
		_nextStep = _deltasMs.Length;
	}

	public int Arm()
	{
		_nextStep = 0;
		return _deltasMs[0];
	}

	public BurstStep Tick()
	{
		if (!IsRunning)
		{
			return BurstStep.Idle;
		}
		int attempt = _nextStep + 1;
		_nextStep++;
		int? nextDelayMs = ((_nextStep < _deltasMs.Length) ? new int?(_deltasMs[_nextStep]) : ((int?)null));
		return new BurstStep(attempt, nextDelayMs);
	}

	public void Cancel()
	{
		_nextStep = _deltasMs.Length;
	}
}
