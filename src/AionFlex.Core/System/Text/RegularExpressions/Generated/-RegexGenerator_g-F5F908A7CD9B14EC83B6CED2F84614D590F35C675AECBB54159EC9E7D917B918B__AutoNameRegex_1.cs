using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.15.405")]
internal sealed class _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__AutoNameRegex_1 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				if (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan))
				{
					runtextpos = inputSpan.Length;
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 9 && num == 0)
				{
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
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int capturePosition = 0;
				int capturePosition2 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int num9 = 0;
				int start2 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				if (num != 0)
				{
					UncaptureUntil(0);
					return false;
				}
				num9 = num;
				num6 = num;
				num5 = Crawlpos();
				if (span.StartsWith("Session "))
				{
					num7 = num;
					capturePosition = Crawlpos();
					num += 8;
					span = inputSpan.Slice(num);
					int i;
					for (i = 0; i < 4 && (uint)i < (uint)span.Length && char.IsDigit(span[i]); i++)
					{
					}
					if (i == 0)
					{
						goto IL_00c2;
					}
					span = span.Slice(i);
					num += i;
					num3 = 0;
					goto IL_0185;
				}
				goto IL_018c;
				IL_0216:
				num = num8;
				span = inputSpan.Slice(num);
				UncaptureUntil(capturePosition2);
				if (!span.StartsWith("Wed"))
				{
					goto IL_0254;
				}
				num4 = 2;
				num += 3;
				span = inputSpan.Slice(num);
				goto IL_03d5;
				IL_00c2:
				num = num7;
				span = inputSpan.Slice(num);
				UncaptureUntil(capturePosition);
				num += 8;
				span = inputSpan.Slice(num);
				int j;
				for (j = 0; j < 2 && (uint)j < (uint)span.Length && char.IsDigit(span[j]); j++)
				{
				}
				if (j != 0)
				{
					span = span.Slice(j);
					num += j;
					if ((uint)span.Length >= 3u && span[0] == ':' && char.IsDigit(span[1]) && char.IsDigit(span[2]))
					{
						num3 = 1;
						num += 3;
						span = inputSpan.Slice(num);
						goto IL_0185;
					}
				}
				goto IL_018c;
				IL_01d8:
				num = num8;
				span = inputSpan.Slice(num);
				UncaptureUntil(capturePosition2);
				if (!span.StartsWith("Tue"))
				{
					goto IL_0216;
				}
				num4 = 1;
				num += 3;
				span = inputSpan.Slice(num);
				goto IL_03d5;
				IL_0292:
				num = num8;
				span = inputSpan.Slice(num);
				UncaptureUntil(capturePosition2);
				if (!span.StartsWith("Fri"))
				{
					goto IL_02d0;
				}
				num4 = 4;
				num += 3;
				span = inputSpan.Slice(num);
				goto IL_03d5;
				IL_0254:
				num = num8;
				span = inputSpan.Slice(num);
				UncaptureUntil(capturePosition2);
				if (!span.StartsWith("Thu"))
				{
					goto IL_0292;
				}
				num4 = 3;
				num += 3;
				span = inputSpan.Slice(num);
				goto IL_03d5;
				IL_018c:
				num = num6;
				span = inputSpan.Slice(num);
				UncaptureUntil(num5);
				start2 = num;
				num8 = num;
				capturePosition2 = Crawlpos();
				if (!span.StartsWith("Mon"))
				{
					goto IL_01d8;
				}
				num4 = 0;
				num += 3;
				span = inputSpan.Slice(num);
				goto IL_03d5;
				IL_02d0:
				num = num8;
				span = inputSpan.Slice(num);
				UncaptureUntil(capturePosition2);
				if (span.IsEmpty || span[0] != 'S')
				{
					UncaptureUntil(0);
					return false;
				}
				if ((uint)span.Length < 2u)
				{
					UncaptureUntil(0);
					return false;
				}
				switch (span[1])
				{
				case 'a':
					if ((uint)span.Length < 3u || span[2] != 't')
					{
						UncaptureUntil(0);
						return false;
					}
					num += 3;
					span = inputSpan.Slice(num);
					break;
				case 'u':
					if ((uint)span.Length < 3u || span[2] != 'n')
					{
						UncaptureUntil(0);
						return false;
					}
					num += 3;
					span = inputSpan.Slice(num);
					break;
				default:
					UncaptureUntil(0);
					return false;
				}
				num4 = 5;
				goto IL_03d5;
				IL_03d5:
				Capture(2, start2, num);
				if (!span.IsEmpty && span[0] == ' ')
				{
					num++;
					span = inputSpan.Slice(num);
					int k;
					for (k = 0; k < 2 && (uint)k < (uint)span.Length && char.IsDigit(span[k]); k++)
					{
					}
					if (k != 0)
					{
						span = span.Slice(k);
						num += k;
						if ((uint)span.Length >= 12u && span[0] == ' ' && char.IsAsciiLetterUpper(span[1]) && char.IsAsciiLetterLower(span[2]) && char.IsAsciiLetterLower(span[3]) && span.Slice(4).StartsWith(" · ") && char.IsDigit(span[7]) && char.IsDigit(span[8]) && span[9] == ':' && char.IsDigit(span[10]) && char.IsDigit(span[11]))
						{
							num2 = 1;
							num += 12;
							span = inputSpan.Slice(num);
							goto IL_0545;
						}
					}
				}
				goto IL_039e;
				IL_0185:
				num2 = 0;
				goto IL_0545;
				IL_0545:
				while (true)
				{
					Capture(1, num9, num);
					if (num < inputSpan.Length - 1 || ((uint)num < (uint)inputSpan.Length && inputSpan[num] != '\n'))
					{
						if (_003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_hasTimeout)
						{
							CheckTimeout();
						}
						if (num2 == 0)
						{
							break;
						}
						if (num2 != 1)
						{
							continue;
						}
						goto IL_039e;
					}
					runtextpos = num;
					Capture(0, start, num);
					return true;
				}
				if (_003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_hasTimeout)
				{
					CheckTimeout();
				}
				if (num3 == 0)
				{
					goto IL_00c2;
				}
				if (num3 != 1)
				{
					goto IL_0185;
				}
				goto IL_018c;
				IL_039e:
				if (_003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_hasTimeout)
				{
					CheckTimeout();
				}
				switch (num4)
				{
				case 0:
					break;
				case 1:
					goto IL_0216;
				case 2:
					goto IL_0254;
				case 3:
					goto IL_0292;
				case 4:
					goto IL_02d0;
				case 5:
					UncaptureUntil(0);
					return false;
				default:
					goto IL_03d5;
				}
				goto IL_01d8;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void UncaptureUntil(int num10)
				{
					while (Crawlpos() > num10)
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

	internal static readonly _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__AutoNameRegex_1 Instance = new _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__AutoNameRegex_1();

	private _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__AutoNameRegex_1()
	{
		pattern = "^(Session \\d{1,4}|Session \\d{1,2}:\\d{2}|(Mon|Tue|Wed|Thu|Fri|Sat|Sun) \\d{1,2} [A-Z][a-z]{2} · \\d{2}:\\d{2})$";
		roptions = RegexOptions.None;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF5F908A7CD9B14EC83B6CED2F84614D590F35C675AECBB54159EC9E7D917B918B__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 3;
	}
}
