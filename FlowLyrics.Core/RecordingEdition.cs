using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlowLyrics.Core;

/// <summary>Independent edition dimensions; the word "version" is never an identity.</summary>
public static class RecordingEdition
{
	private static readonly (string Kind, string Pattern)[] Markers =
	[
		("live", @"\blive\b|ライブ"),
		("acoustic", @"\bacoustic\b|アコースティック"),
		("piano", @"\bpiano\b|ピアノ"),
		("instrumental", @"\binstrumental\b|インスト(?:ゥルメンタル)?"),
		("karaoke", @"\bkaraoke\b|\boff[ -]?vocal\b|カラオケ"),
		("remix", @"\b(?:remix|mix)\b|リミックス"),
		("radio-edit", @"\bradio[ -]+(?:edit|version|ver\.?)\b"),
		("single-version", @"\bsingle[ -]+(?:version|ver\.?)\b"),
		("extended", @"\bextended(?:[ -]+(?:version|ver\.?|mix))?\b"),
		("sped-up", @"\bsped[ -]+up\b"),
		("slowed", @"\bslowed(?:[ -]+down)?\b"),
		("nightcore", @"\bnightcore\b"),
		("remaster", @"\bremaster(?:ed)?\b|リマスター"),
		("re-recorded", @"\bre[ -]?record(?:ed|ing)\b"),
		("clean", @"\bclean\b"),
		("explicit", @"\bexplicit\b"),
		("tv-size", @"\btv[ -]?(?:size|version|ver\.?)\b|テレビサイズ|tvサイズ"),
		("japanese", @"\b(?:japanese|jpn|jp)[ -]+(?:version|ver\.?)\b|日本語(?:版|バージョン)"),
		("english", @"\b(?:english|eng)[ -]+(?:version|ver\.?)\b|英語(?:版|バージョン)"),
		("chinese", @"\b(?:chinese|mandarin|chn)[ -]+(?:version|ver\.?)\b|中国語(?:版|バージョン)"),
		("korean", @"\b(?:korean|kor)[ -]+(?:version|ver\.?)\b|韓国語(?:版|バージョン)"),
		("from-vault", @"\bfrom\s+the\s+vault\b"),
		("cover", @"\bcover\b|カバー")
	];

	public static HashSet<string> Signature(string? title, string? album = null)
	{
		HashSet<string> signature = new(StringComparer.Ordinal);
		foreach (string value in new[] { title ?? string.Empty, album ?? string.Empty })
		{
			string normalized = MetadataNormalizer.NormalizeWhitespace(value).ToLowerInvariant();
			// Credits can themselves contain words such as "Live". They are not editions.
			normalized = Regex.Replace(normalized, @"\((?:feat\.?|ft\.?|featuring)\s+[^()]*\)", "", RegexOptions.IgnoreCase);
			foreach ((string kind, string pattern) in Markers)
			{
				if (Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) signature.Add(kind);
			}
			foreach (string annotation in Annotations(normalized))
			{
				if (Regex.IsMatch(annotation, @"^(?:feat\.?|ft\.?|featuring)\s", RegexOptions.IgnoreCase)) continue;
				string ownAnnotation = WithoutNestedAnnotations(annotation);
				if (Regex.IsMatch(annotation, @"\b(?:remix|mix)\b|リミックス", RegexOptions.IgnoreCase))
					signature.Add("mix-credit:" + MetadataNormalizer.ComparisonKey(annotation));
				if (Regex.IsMatch(annotation, @"\bremaster(?:ed)?\b|リマスター", RegexOptions.IgnoreCase))
				{
					Match year = Regex.Match(annotation, @"\b(?:19|20)\d{2}\b");
					if (year.Success) signature.Add("remaster-year:" + year.Value);
				}
				if (Regex.IsMatch(ownAnnotation, @"\b(?:version|ver\.?)\b", RegexOptions.IgnoreCase)
					&& !Markers.Any(marker => Regex.IsMatch(ownAnnotation, marker.Pattern, RegexOptions.IgnoreCase)))
					signature.Add("named-version:" + MetadataNormalizer.ComparisonKey(ownAnnotation));
			}
		}
		return signature;
	}

	public static bool HasMarker(string value) => Signature(value).Count > 0
		|| Regex.IsMatch(value, @"\b(?:version|ver\.?|edit|feat\.?|ft\.?|featuring)\b", RegexOptions.IgnoreCase);

	private static string WithoutNestedAnnotations(string value)
	{
		string previous;
		do
		{
			previous = value;
			value = Regex.Replace(value, @"\([^()]*\)|\[[^\[\]]*\]", " ");
		} while (value != previous);
		return value.Trim();
	}

	private static IEnumerable<string> Annotations(string value)
	{
		// Keep both inner and outer annotations, so named nested mixes retain their credit.
		Stack<int> starts = new();
		for (int index = 0; index < value.Length; index++)
		{
			if (value[index] is '(' or '[') starts.Push(index + 1);
			else if (value[index] is ')' or ']' && starts.Count > 0)
			{
				int start = starts.Pop();
				yield return value[start..index].Trim();
			}
		}
		Match suffix = Regex.Match(value, @"\s[-–—]\s(.+)$");
		// Nested edition annotations were already yielded; the surrounding song
		// title in "Artist - Song (Named Remix)" is not part of the remix credit.
		if (suffix.Success) yield return WithoutNestedAnnotations(suffix.Groups[1].Value);
		if (!value.Contains('(') && !value.Contains('[') && !suffix.Success) yield return value;
	}
}
