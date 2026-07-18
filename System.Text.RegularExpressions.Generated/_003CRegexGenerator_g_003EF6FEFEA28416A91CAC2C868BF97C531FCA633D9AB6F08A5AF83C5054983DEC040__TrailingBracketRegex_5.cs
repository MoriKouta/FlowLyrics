using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.32716")]
internal sealed class _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__TrailingBracketRegex_5 : Regex
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
				if (num <= inputSpan.Length - 3)
				{
					ReadOnlySpan<char> span = inputSpan.Slice(num);
					int num2 = span.IndexOfAny("([【（".AsSpan());
					if (num2 >= 0)
					{
						int num3 = num2 - 1;
						while ((uint)num3 < (uint)span.Length && char.IsWhiteSpace(span[num3]))
						{
							num3--;
						}
						runtextpos = num + num3 + 1;
						runtrackpos = num + num2;
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
				int num2 = 0;
				int num3 = 0;
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				num = runtrackpos;
				readOnlySpan = inputSpan.Slice(num);
				if (runtextpos < num)
				{
					runtextpos = num;
				}
				char c;
				if (readOnlySpan.IsEmpty || (((c = readOnlySpan[0]) < '\u0080') ? (("\0\0Ā\0\0ࠀ\0\0"[(int)c >> 4] & (1 << (c & 0xF))) == 0) : (!RegexRunner.CharInClass(c, "\0\b\0()[\\【】（）"))))
				{
					return false;
				}
				int num4 = readOnlySpan.Slice(1).IndexOfAny(")]】）".AsSpan());
				if (num4 < 0)
				{
					num4 = readOnlySpan.Length - 1;
				}
				if (num4 == 0)
				{
					return false;
				}
				readOnlySpan = readOnlySpan.Slice(num4);
				num += num4;
				if ((uint)readOnlySpan.Length < 2u || (((c = readOnlySpan[1]) < '\u0080') ? (("\0\0Ȁ\0\0\u2000\0\0"[(int)c >> 4] & (1 << (c & 0xF))) == 0) : (!RegexRunner.CharInClass(c, "\0\b\0)*]^】〒）＊"))))
				{
					return false;
				}
				num += 2;
				readOnlySpan = inputSpan.Slice(num);
				num2 = num;
				int i;
				for (i = 0; (uint)i < (uint)readOnlySpan.Length && char.IsWhiteSpace(readOnlySpan[i]); i++)
				{
				}
				readOnlySpan = readOnlySpan.Slice(i);
				num += i;
				num3 = num;
				while (num < inputSpan.Length - 1 || ((uint)num < (uint)inputSpan.Length && inputSpan[num] != '\n'))
				{
					if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num2 >= num3)
					{
						return false;
					}
					num = --num3;
					readOnlySpan = inputSpan.Slice(num);
				}
				runtextpos = num;
				Capture(0, start, num);
				return true;
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__TrailingBracketRegex_5 Instance = new _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__TrailingBracketRegex_5();

	private _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__TrailingBracketRegex_5()
	{
		pattern = "\\s*[\\(\\[（【][^\\)\\]）】]+[\\)\\]）】]\\s*$";
		roptions = RegexOptions.Compiled;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
