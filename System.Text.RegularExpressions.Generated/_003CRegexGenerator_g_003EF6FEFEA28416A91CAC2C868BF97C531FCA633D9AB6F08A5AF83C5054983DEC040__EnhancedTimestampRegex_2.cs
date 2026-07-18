using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.32716")]
internal sealed class _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EnhancedTimestampRegex_2 : Regex
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
					if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 6)
				{
					ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
					int num2;
					for (num2 = 0; num2 < readOnlySpan.Length - 5; num2++)
					{
						int num3 = readOnlySpan.Slice(num2).IndexOf('<');
						if (num3 < 0)
						{
							break;
						}
						num2 += num3;
						if ((uint)(num2 + 1) >= (uint)readOnlySpan.Length)
						{
							break;
						}
						if (char.IsDigit(readOnlySpan[num2 + 1]))
						{
							runtextpos = num + num2;
							return true;
						}
					}
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				int pos = 0;
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				if (readOnlySpan.IsEmpty || readOnlySpan[0] != '<')
				{
					return false;
				}
				num++;
				readOnlySpan = inputSpan.Slice(num);
				int i;
				for (i = 0; i < 3 && (uint)i < (uint)readOnlySpan.Length && char.IsDigit(readOnlySpan[i]); i++)
				{
				}
				if (i == 0)
				{
					return false;
				}
				readOnlySpan = readOnlySpan.Slice(i);
				num += i;
				if ((uint)readOnlySpan.Length < 3u || readOnlySpan[0] != ':' || !char.IsDigit(readOnlySpan[1]) || !char.IsDigit(readOnlySpan[2]))
				{
					return false;
				}
				num += 3;
				readOnlySpan = inputSpan.Slice(num);
				num2 = 0;
				while (true)
				{
					_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.StackPush(ref runstack, ref pos, num);
					num2++;
					char c;
					if (!readOnlySpan.IsEmpty && ((c = readOnlySpan[0]) == '.' || c == ':'))
					{
						num++;
						readOnlySpan = inputSpan.Slice(num);
						int j;
						for (j = 0; j < 3 && (uint)j < (uint)readOnlySpan.Length && char.IsDigit(readOnlySpan[j]); j++)
						{
						}
						if (j != 0)
						{
							readOnlySpan = readOnlySpan.Slice(j);
							num += j;
							if (num2 == 0)
							{
								continue;
							}
							goto IL_0150;
						}
					}
					goto IL_0166;
					IL_0150:
					if (!readOnlySpan.IsEmpty && readOnlySpan[0] == '>')
					{
						break;
					}
					goto IL_0166;
					IL_0166:
					if (--num2 < 0)
					{
						return false;
					}
					num = runstack[--pos];
					readOnlySpan = inputSpan.Slice(num);
					goto IL_0150;
				}
				Capture(0, start, runtextpos = num + 1);
				return true;
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EnhancedTimestampRegex_2 Instance = new _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EnhancedTimestampRegex_2();

	private _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EnhancedTimestampRegex_2()
	{
		pattern = "<\\d{1,3}:\\d{2}(?:[\\.:]\\d{1,3})?>";
		roptions = RegexOptions.Compiled;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
