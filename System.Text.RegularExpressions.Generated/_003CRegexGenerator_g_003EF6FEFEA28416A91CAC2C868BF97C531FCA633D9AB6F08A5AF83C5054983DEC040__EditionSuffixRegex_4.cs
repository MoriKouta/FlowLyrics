using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.32716")]
internal sealed class _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EditionSuffixRegex_4 : Regex
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
					ReadOnlySpan<char> span = inputSpan.Slice(num);
					int num2 = span.IndexOfAny('-', '–', '—');
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
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int pos = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				num = runtrackpos;
				span = inputSpan.Slice(num);
				if (runtextpos < num)
				{
					runtextpos = num;
				}
				char c;
				if (span.IsEmpty || ((c = span[0]) != '-' && c != '–' && c != '—'))
				{
					return false;
				}
				num++;
				span = inputSpan.Slice(num);
				num4 = num;
				int i;
				for (i = 0; (uint)i < (uint)span.Length && char.IsWhiteSpace(span[i]); i++)
				{
				}
				span = span.Slice(i);
				num += i;
				num5 = num;
				while (true)
				{
					num7 = 0;
					while (true)
					{
						_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.StackPush(ref runstack, ref pos, num);
						num7++;
						if ((uint)span.Length < 4u || !char.IsDigit(span[0]) || !char.IsDigit(span[1]) || !char.IsDigit(span[2]) || !char.IsDigit(span[3]))
						{
							goto IL_0429;
						}
						int j;
						for (j = 4; (uint)j < (uint)span.Length && char.IsWhiteSpace(span[j]); j++)
						{
						}
						span = span.Slice(j);
						num += j;
						if (num7 == 0)
						{
							continue;
						}
						goto IL_0499;
						IL_0175:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 7u && span.StartsWith("version".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 3;
							num += 7;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_02c7;
						IL_0608:
						num = num3;
						span = inputSpan.Slice(num);
						if (span.StartsWith("リマスター".AsSpan()))
						{
							num2 = 11;
							num += 5;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0728;
						IL_0728:
						num = num3;
						span = inputSpan.Slice(num);
						if (span.StartsWith("ライブ".AsSpan()))
						{
							num2 = 12;
							num += 3;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_030c;
						IL_02c7:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 3u && span.StartsWith("mix".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 4;
							num += 3;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_03e4;
						IL_01f8:
						while (true)
						{
							num6 = num;
							while (true)
							{
								if (num < inputSpan.Length - 1 || ((uint)num < (uint)inputSpan.Length && inputSpan[num] != '\n'))
								{
									if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
									{
										CheckTimeout();
									}
									num = num6;
									span = inputSpan.Slice(num);
									if (span.IsEmpty || span[0] == '\n')
									{
										break;
									}
									num++;
									span = inputSpan.Slice(num);
									num6 = num;
									continue;
								}
								runtextpos = num;
								Capture(0, start, num);
								return true;
							}
							if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
							{
								CheckTimeout();
							}
							switch (num2)
							{
							case 2:
								break;
							case 8:
								goto IL_01b7;
							default:
								continue;
							case 3:
								goto IL_02c7;
							case 12:
								goto IL_030c;
							case 7:
								goto IL_0344;
							case 4:
								goto IL_03e4;
							case 14:
								goto IL_0429;
							case 9:
								goto IL_0451;
							case 0:
								goto IL_0519;
							case 5:
								goto IL_0544;
							case 13:
								goto IL_058b;
							case 10:
								goto IL_0608;
							case 6:
								goto IL_0640;
							case 1:
								goto IL_06dc;
							case 11:
								goto IL_0728;
							}
							break;
						}
						goto IL_0175;
						IL_01b7:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 6u && span.StartsWith("slowed".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 9;
							num += 6;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0451;
						IL_03e4:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 8u && span.StartsWith("acoustic".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 5;
							num += 8;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0544;
						IL_0429:
						if (--num7 < 0)
						{
							break;
						}
						num = runstack[--pos];
						span = inputSpan.Slice(num);
						goto IL_0499;
						IL_030c:
						num = num3;
						span = inputSpan.Slice(num);
						if (span.StartsWith("カラオケ".AsSpan()))
						{
							num2 = 13;
							num += 4;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_058b;
						IL_0499:
						num3 = num;
						if ((uint)span.Length >= 8u && span.StartsWith("remaster".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num += 8;
							span = inputSpan.Slice(num);
							num8 = 0;
							while (true)
							{
								_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.StackPush(ref runstack, ref pos, num);
								num8++;
								if ((uint)span.Length < 2u || !span.StartsWith("ed".AsSpan(), StringComparison.OrdinalIgnoreCase))
								{
									break;
								}
								num += 2;
								span = inputSpan.Slice(num);
								if (num8 == 0)
								{
									continue;
								}
								goto IL_0721;
							}
							goto IL_0519;
						}
						goto IL_05c3;
						IL_0344:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 4u && span.StartsWith("sped".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							int k;
							for (k = 4; (uint)k < (uint)span.Length && char.IsWhiteSpace(span[k]); k++)
							{
							}
							span = span.Slice(k);
							num += k;
							if ((uint)span.Length >= 2u && span.StartsWith("up".AsSpan(), StringComparison.OrdinalIgnoreCase))
							{
								num2 = 8;
								num += 2;
								span = inputSpan.Slice(num);
								goto IL_01f8;
							}
						}
						goto IL_01b7;
						IL_0544:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 12u && span.StartsWith("instrumental".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 6;
							num += 12;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0640;
						IL_0451:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 9u && span.StartsWith("nightcore".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 10;
							num += 9;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0608;
						IL_05c3:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 4u && span.StartsWith("live".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							num2 = 1;
							num += 4;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_06dc;
						IL_0519:
						if (--num8 < 0)
						{
							goto IL_05c3;
						}
						num = runstack[--pos];
						span = inputSpan.Slice(num);
						goto IL_0721;
						IL_0640:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length >= 7u && (((c = span[0]) | 0x20) == 107 || c == 'K') && span.Slice(1).StartsWith("arao".AsSpan(), StringComparison.OrdinalIgnoreCase) && (((c = span[5]) | 0x20) == 107 || c == 'K') && (span[6] | 0x20) == 101)
						{
							num2 = 7;
							num += 7;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0344;
						IL_06dc:
						num = num3;
						span = inputSpan.Slice(num);
						if ((uint)span.Length < 4u || !span.StartsWith("edit".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							goto IL_0175;
						}
						num2 = 2;
						num += 4;
						span = inputSpan.Slice(num);
						goto IL_01f8;
						IL_058b:
						num = num3;
						span = inputSpan.Slice(num);
						if (span.StartsWith("別バージョン".AsSpan()))
						{
							num2 = 14;
							num += 6;
							span = inputSpan.Slice(num);
							goto IL_01f8;
						}
						goto IL_0429;
						IL_0721:
						num2 = 0;
						goto IL_01f8;
					}
					if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num4 >= num5)
					{
						break;
					}
					num = --num5;
					span = inputSpan.Slice(num);
				}
				return false;
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EditionSuffixRegex_4 Instance = new _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EditionSuffixRegex_4();

	private _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__EditionSuffixRegex_4()
	{
		pattern = "\\s*[-–—]\\s*(?:\\d{4}\\s*)?(?:remaster(?:ed)?|live|edit|version|mix|acoustic|instrumental|karaoke|sped\\s*up|slowed|nightcore|リマスター|ライブ|カラオケ|別バージョン).*?$";
		roptions = RegexOptions.IgnoreCase | RegexOptions.Compiled;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
