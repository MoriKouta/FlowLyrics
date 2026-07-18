using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.32716")]
internal sealed class _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSeparatorRegex_6 : Regex
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
				if (num <= inputSpan.Length - 4)
				{
					while (true)
					{
						ReadOnlySpan<char> span = inputSpan.Slice(num);
						int num2 = span.IndexOfAny('F', 'f');
						if (num2 < 0)
						{
							break;
						}
						int num3 = num2 - 1;
						while ((uint)num3 < (uint)span.Length && char.IsWhiteSpace(span[num3]))
						{
							num3--;
						}
						if (num2 - num3 - 1 < 1)
						{
							num += num2 + 1;
							continue;
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
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				num = runtrackpos;
				readOnlySpan = inputSpan.Slice(num);
				if (runtextpos < num)
				{
					runtextpos = num;
				}
				if (readOnlySpan.IsEmpty || (readOnlySpan[0] | 0x20) != 102)
				{
					return false;
				}
				if ((uint)readOnlySpan.Length < 2u)
				{
					return false;
				}
				switch (readOnlySpan[1])
				{
				case 'E':
				case 'e':
					if ((uint)readOnlySpan.Length < 4u || !readOnlySpan.Slice(2).StartsWith("at".AsSpan(), StringComparison.OrdinalIgnoreCase))
					{
						return false;
					}
					if ((uint)readOnlySpan.Length > 4u && readOnlySpan[4] == '.')
					{
						readOnlySpan = readOnlySpan.Slice(1);
						num++;
					}
					num += 4;
					readOnlySpan = inputSpan.Slice(num);
					break;
				case 'T':
				case 't':
					if ((uint)readOnlySpan.Length > 2u && readOnlySpan[2] == '.')
					{
						readOnlySpan = readOnlySpan.Slice(1);
						num++;
					}
					num += 2;
					readOnlySpan = inputSpan.Slice(num);
					break;
				default:
					return false;
				}
				int i;
				for (i = 0; (uint)i < (uint)readOnlySpan.Length && char.IsWhiteSpace(readOnlySpan[i]); i++)
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

	internal static readonly _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSeparatorRegex_6 Instance = new _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSeparatorRegex_6();

	private _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSeparatorRegex_6()
	{
		pattern = "\\s+(?:feat\\.?|ft\\.?)\\s+";
		roptions = RegexOptions.IgnoreCase | RegexOptions.Compiled;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
