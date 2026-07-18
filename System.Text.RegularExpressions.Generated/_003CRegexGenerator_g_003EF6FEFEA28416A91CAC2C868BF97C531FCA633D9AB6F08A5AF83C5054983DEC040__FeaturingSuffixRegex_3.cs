using System.CodeDom.Compiler;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "10.0.14.32716")]
internal sealed class _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSuffixRegex_3 : Regex
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
					int num2 = inputSpan.Slice(num).IndexOfNonAsciiOrAny_8A46238BE6649C5856DAFF9874446EFF73C5C3E27BAE3C52E3AA2858AB5EE2EE();
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
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				ReadOnlySpan<char> readOnlySpan = inputSpan.Slice(num);
				num2 = num;
				int i;
				for (i = 0; (uint)i < (uint)readOnlySpan.Length && char.IsWhiteSpace(readOnlySpan[i]); i++)
				{
				}
				readOnlySpan = readOnlySpan.Slice(i);
				num += i;
				num3 = num;
				while (true)
				{
					if (runtextpos < num)
					{
						runtextpos = num;
					}
					char c;
					if (!readOnlySpan.IsEmpty && (((c = readOnlySpan[0]) < '\u0080') ? ((byte)("\0\0Ā\0\0ࠀ\0\0"[(int)c >> 4] & (1 << (c & 0xF))) != 0) : RegexRunner.CharInClass(c, "\0\b\0()[\\【】（）")))
					{
						readOnlySpan = readOnlySpan.Slice(1);
						num++;
					}
					int j;
					for (j = 0; (uint)j < (uint)readOnlySpan.Length && char.IsWhiteSpace(readOnlySpan[j]); j++)
					{
					}
					readOnlySpan = readOnlySpan.Slice(j);
					num += j;
					if (!readOnlySpan.IsEmpty && (readOnlySpan[0] | 0x20) == 102 && (uint)readOnlySpan.Length >= 2u)
					{
						char c2 = readOnlySpan[1];
						if ((uint)c2 <= 84u)
						{
							if (c2 != 'E')
							{
								if (c2 == 'T')
								{
									goto IL_0162;
								}
								goto IL_0342;
							}
						}
						else if (c2 != 'e')
						{
							if (c2 == 't')
							{
								goto IL_0162;
							}
							goto IL_0342;
						}
						if ((uint)readOnlySpan.Length >= 4u && readOnlySpan.Slice(2).StartsWith("at".AsSpan(), StringComparison.OrdinalIgnoreCase))
						{
							if ((uint)readOnlySpan.Length > 4u && readOnlySpan[4] == '.')
							{
								readOnlySpan = readOnlySpan.Slice(1);
								num++;
							}
							num += 4;
							readOnlySpan = inputSpan.Slice(num);
							goto IL_0195;
						}
					}
					goto IL_0342;
					IL_0195:
					num4 = num;
					int k;
					for (k = 0; (uint)k < (uint)readOnlySpan.Length && char.IsWhiteSpace(readOnlySpan[k]); k++)
					{
					}
					if (k != 0)
					{
						readOnlySpan = readOnlySpan.Slice(k);
						num += k;
						num5 = num;
						num4++;
						while (true)
						{
							num8 = num;
							while (true)
							{
								if (!readOnlySpan.IsEmpty && (((c = readOnlySpan[0]) < '\u0080') ? ((byte)("\0\0Ȁ\0\0\u2000\0\0"[(int)c >> 4] & (1 << (c & 0xF))) != 0) : RegexRunner.CharInClass(c, "\0\b\0)*]^】〒）＊")))
								{
									readOnlySpan = readOnlySpan.Slice(1);
									num++;
								}
								num6 = num;
								int l;
								for (l = 0; (uint)l < (uint)readOnlySpan.Length && char.IsWhiteSpace(readOnlySpan[l]); l++)
								{
								}
								readOnlySpan = readOnlySpan.Slice(l);
								num += l;
								num7 = num;
								while (true)
								{
									if (num < inputSpan.Length - 1 || ((uint)num < (uint)inputSpan.Length && inputSpan[num] != '\n'))
									{
										if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
										{
											CheckTimeout();
										}
										if (num6 >= num7)
										{
											break;
										}
										num = --num7;
										readOnlySpan = inputSpan.Slice(num);
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
								num = num8;
								readOnlySpan = inputSpan.Slice(num);
								if (readOnlySpan.IsEmpty || readOnlySpan[0] == '\n')
								{
									break;
								}
								num++;
								readOnlySpan = inputSpan.Slice(num);
								num8 = num;
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
							readOnlySpan = inputSpan.Slice(num);
						}
					}
					goto IL_0342;
					IL_0342:
					if (_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num2 >= num3)
					{
						break;
					}
					num = --num3;
					readOnlySpan = inputSpan.Slice(num);
					continue;
					IL_0162:
					if ((uint)readOnlySpan.Length > 2u && readOnlySpan[2] == '.')
					{
						readOnlySpan = readOnlySpan.Slice(1);
						num++;
					}
					num += 2;
					readOnlySpan = inputSpan.Slice(num);
					goto IL_0195;
				}
				return false;
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSuffixRegex_3 Instance = new _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSuffixRegex_3();

	private _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__FeaturingSuffixRegex_3()
	{
		pattern = "\\s*[\\(\\[（【]?\\s*(?:feat\\.?|ft\\.?)\\s+.*?[\\)\\]）】]?\\s*$";
		roptions = RegexOptions.IgnoreCase | RegexOptions.Compiled;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EF6FEFEA28416A91CAC2C868BF97C531FCA633D9AB6F08A5AF83C5054983DEC040__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 1;
	}
}
