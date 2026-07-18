using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public static class LrcParser
{
	private static readonly Regex TimestampPattern = new Regex("\\[(?<minute>\\d{1,3}):(?<second>\\d{2})(?:[\\.:](?<fraction>\\d{1,3}))?\\]", RegexOptions.Compiled);

	private static readonly Regex OffsetPattern = new Regex("^\\[offset:(?<offset>[+-]?\\d+)\\]$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex EnhancedTimestampPattern = new Regex("<\\d{1,3}:\\d{2}(?:[\\.:]\\d{1,3})?>", RegexOptions.Compiled);

	private static Regex TimestampRegex()
	{
		return TimestampPattern;
	}

	private static Regex OffsetRegex()
	{
		return OffsetPattern;
	}

	private static Regex EnhancedTimestampRegex()
	{
		return EnhancedTimestampPattern;
	}

	public static IReadOnlyList<LyricLine> Parse(string? lrc)
	{
		if (string.IsNullOrWhiteSpace(lrc))
		{
			return Array.Empty<LyricLine>();
		}
		int num = 0;
		List<LyricLine> list = new List<LyricLine>();
		string[] array = lrc.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].TrimEnd();
			Match match = OffsetRegex().Match(text.Trim());
			if (match.Success && int.TryParse(match.Groups["offset"].Value, out var result))
			{
				num = Math.Clamp(result, -30000, 30000);
				continue;
			}
			MatchCollection matchCollection = TimestampRegex().Matches(text);
			if (matchCollection.Count == 0)
			{
				continue;
			}
			string input = TimestampRegex().Replace(text, string.Empty);
			input = EnhancedTimestampRegex().Replace(input, string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(input))
			{
				input = "♪";
			}
			foreach (Match item in matchCollection)
			{
				if (int.TryParse(item.Groups["minute"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result2) && int.TryParse(item.Groups["second"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result3))
				{
					int num2 = ParseFraction(item.Groups["fraction"].Value);
					TimeSpan timeSpan = TimeSpan.FromMinutes(result2) + TimeSpan.FromSeconds(result3) + TimeSpan.FromMilliseconds(num2 + num);
					if (timeSpan < TimeSpan.Zero)
					{
						timeSpan = TimeSpan.Zero;
					}
					list.Add(new LyricLine(timeSpan, input));
				}
			}
		}
		return (from line in list.OrderBy((LyricLine line) => line.Time).ThenBy<LyricLine, string>((LyricLine line) => line.Text, StringComparer.Ordinal)
			group line by new { line.Time, line.Text } into @group
			select @group.First()).ToArray();
	}

	private static int ParseFraction(string value)
	{
		if (string.IsNullOrEmpty(value) || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			return 0;
		}
		return value.Length switch
		{
			1 => result * 100, 
			2 => result * 10, 
			_ => result, 
		};
	}
}
