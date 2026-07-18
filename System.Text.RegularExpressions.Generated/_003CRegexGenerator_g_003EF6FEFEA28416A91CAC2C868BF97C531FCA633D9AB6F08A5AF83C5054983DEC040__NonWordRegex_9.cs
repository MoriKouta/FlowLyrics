using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.32716")]
internal sealed class _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__NonWordRegex_9 : Regex
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
				if ((uint)num < (uint)inputSpan.Length)
				{
					int num2 = inputSpan.Slice(num).IndexOfNonAsciiOrAny_453562F669B20D6106B6D276DCDB27F8890EDDDC57F6DED4861283505703DEF5();
					if (num2 >= 0)
					{
						runtextpos = num + num2;
						return true;
					}
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				int i;
				for (i = 0; (uint)i < (uint)readOnlySpan.Length && (0x71F & (1 << (int)char.GetUnicodeCategory(readOnlySpan[i]))) == 0; i++)
				{
				}
				if (i == 0)
				{
					return false;
				}
				readOnlySpan = readOnlySpan.Slice(i);
				Capture(0, start, runtextpos = num + i);
				return true;
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__NonWordRegex_9 Instance = new _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__NonWordRegex_9();

	private _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__NonWordRegex_9()
	{
		pattern = "[^\\p{L}\\p{N}]+";
		roptions = RegexOptions.Compiled;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
