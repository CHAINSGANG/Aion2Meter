using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.15.405")]
internal sealed class _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__SavedAt_0 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				while (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan) && runtextpos != inputSpan.Length)
				{
					runtextpos++;
					if (_003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 25)
				{
					if (num < inputSpan.Length - 26)
					{
						runtextpos = inputSpan.Length - 26;
					}
					return true;
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				if (readOnlySpan.IsEmpty || readOnlySpan[0] != '_')
				{
					UncaptureUntil(0);
					return false;
				}
				num++;
				readOnlySpan = inputSpan.Slice(num);
				num2 = num;
				if ((uint)readOnlySpan.Length < 19u || !char.IsDigit(readOnlySpan[0]) || !char.IsDigit(readOnlySpan[1]) || !char.IsDigit(readOnlySpan[2]) || !char.IsDigit(readOnlySpan[3]) || readOnlySpan[4] != '-' || !char.IsDigit(readOnlySpan[5]) || !char.IsDigit(readOnlySpan[6]) || readOnlySpan[7] != '-' || !char.IsDigit(readOnlySpan[8]) || !char.IsDigit(readOnlySpan[9]) || readOnlySpan[10] != '_' || !char.IsDigit(readOnlySpan[11]) || !char.IsDigit(readOnlySpan[12]) || readOnlySpan[13] != '-' || !char.IsDigit(readOnlySpan[14]) || !char.IsDigit(readOnlySpan[15]) || readOnlySpan[16] != '-' || !char.IsDigit(readOnlySpan[17]) || !char.IsDigit(readOnlySpan[18]))
				{
					UncaptureUntil(0);
					return false;
				}
				num += 19;
				readOnlySpan = inputSpan.Slice(num);
				Capture(1, num2, num);
				if (!readOnlySpan.StartsWith(".json"))
				{
					UncaptureUntil(0);
					return false;
				}
				if (6 < readOnlySpan.Length || (5 < readOnlySpan.Length && readOnlySpan[5] != '\n'))
				{
					UncaptureUntil(0);
					return false;
				}
				Capture(0, start, runtextpos = num + 5);
				return true;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void UncaptureUntil(int capturePosition)
				{
					while (Crawlpos() > capturePosition)
					{
						Uncapture();
					}
				}
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__SavedAt_0 Instance = new _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__SavedAt_0();

	private _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__SavedAt_0()
	{
		pattern = "_(\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2})\\.json$";
		roptions = RegexOptions.CultureInvariant;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 2;
	}
}
