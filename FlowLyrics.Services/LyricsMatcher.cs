using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public static class LyricsMatcher
{
	private static readonly Regex WhitespacePattern = new Regex("\\s+", RegexOptions.Compiled);

	private static readonly Regex BracketAliasPattern = new Regex("[\\(\\[【「（［]([^\\)\\]】」）］]+)[\\)\\]】」）］]", RegexOptions.Compiled);

	private static readonly Regex ArtistSeparatorPattern = new Regex("\\s*(?:,|，|、|&|＆|/|／|;|；|×|=|\\bx\\b|\\bfeat(?:uring)?\\.?\\b|\\bft\\.?\\b|\\bstarring\\b)\\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex ComparisonPunctuationPattern = new Regex("[\\-‐‑‒–—―ーｰ・･,，、/／&＆×=+＋:：;；_]+", RegexOptions.Compiled);

	private static readonly Regex RemainingPunctuationPattern = new Regex("[^\\p{L}\\p{N}]+", RegexOptions.Compiled);

	private static readonly Regex EditionPattern = new Regex("\\b(live|instrumental|remix|mix|cover|karaoke|tv\\s*size|acoustic|sped\\s*up|slowed|nightcore|remaster(?:ed)?|edit|version)\\b|ライブ|インスト(?:ゥルメンタル)?|リミックス|カバー|カラオケ|テレビサイズ|tvサイズ|アコースティック|リマスター", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex LrcTagPattern = new Regex("\\[[^\\]]*\\]", RegexOptions.Compiled);

	private static readonly HashSet<string> RomanizedJapaneseHints = new HashSet<string>(StringComparer.Ordinal)
	{
		"ai", "ano", "anata", "boku", "dake", "dare", "demo", "ga", "ima", "itsumo",
		"kara", "kimi", "kono", "kokoro", "mada", "made", "mou", "naka", "nani", "ni",
		"omoidasu", "ore", "sayonara", "sekai", "shiranai", "sora", "sono", "suki", "toki", "uta",
		"wa", "watashi", "wo", "yume", "yori", "zutto"
	};

	public static LyricsCandidate Evaluate(TrackInfo track, LrclibRecord candidate)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>();
		string expectedTitle = NormalizeForComparison(track.Title);
		bool num = ExtractTitleAliases(candidate.TrackName).Any((string alias) => string.Equals(expectedTitle, NormalizeForComparison(alias), StringComparison.Ordinal));
		bool flag = num && !string.Equals(expectedTitle, NormalizeForComparison(candidate.TrackName), StringComparison.Ordinal);
		if (num)
		{
			list.Add(flag ? "Title alias" : "Title");
		}
		else
		{
			list2.Add("Title");
			list3.Add("Title does not match");
		}
		string text = NormalizeForComparison(track.Artist);
		string b = NormalizeForComparison(candidate.ArtistName);
		bool flag2 = text.Length > 0 && string.Equals(text, b, StringComparison.Ordinal);
		HashSet<string> hashSet = ArtistTokens(track.Artist);
		HashSet<string> hashSet2 = ArtistTokens(candidate.ArtistName);
		bool flag3 = hashSet.Count > 0 && hashSet.SetEquals(hashSet2);
		bool flag4 = hashSet.Count > 0 && hashSet2.Count > hashSet.Count && hashSet.IsSubsetOf(hashSet2);
		bool flag5 = !flag2 && !flag3 && !flag4 && IsCrossScriptArtistPair(track.Artist, candidate.ArtistName);
		if (flag2 || flag3 || flag4 || flag5)
		{
			list.Add(flag2 ? "Artist" : (flag4 ? "Primary artist included" : (flag3 ? "Artist tokens" : "Cross-script artist")));
		}
		else
		{
			list2.Add("Artist");
			list3.Add("Artist does not match");
		}
		string text2 = NormalizeForComparison(track.Album);
		string text3 = NormalizeForComparison(candidate.AlbumName);
		bool flag6 = text2.Length > 0 && string.Equals(text2, text3, StringComparison.Ordinal);
		bool flag7 = text2.Length >= 3 && text3.Length >= 3 && (text2.Contains(text3, StringComparison.Ordinal) || text3.Contains(text2, StringComparison.Ordinal));
		if (flag6 || flag7)
		{
			list.Add("Album");
		}
		else if (text2.Length > 0 && text3.Length > 0)
		{
			list2.Add("Album");
		}
		double? num2 = null;
		if (track.Duration > TimeSpan.Zero && candidate.Duration > 0.0)
		{
			num2 = Math.Abs(track.Duration.TotalSeconds - candidate.Duration);
			if (num2 <= 2.0)
			{
				list.Add("Duration");
			}
			else
			{
				list2.Add("Duration");
				list3.Add("Duration exceeds 2 seconds");
			}
		}
		else
		{
			list2.Add("Duration");
			list3.Add("Duration is unavailable");
		}
		HashSet<string> hashSet3 = EditionTokens(track.Title + " " + track.Album);
		HashSet<string> hashSet4 = EditionTokens((candidate.TrackName ?? string.Empty) + " " + (candidate.AlbumName ?? string.Empty));
		bool flag8 = !hashSet3.SetEquals(hashSet4);
		if (flag8)
		{
			list2.Add("Version");
			list3.Add("Version does not match");
		}
		bool flag9 = hashSet3.Contains("instrumental");
		bool flag10 = candidate.Instrumental != flag9;
		if (flag10)
		{
			list2.Add("Instrumental");
			list3.Add("Instrumental status does not match");
		}
		bool flag11 = candidate.Instrumental || !string.IsNullOrWhiteSpace(candidate.SyncedLyrics) || !string.IsNullOrWhiteSpace(candidate.PlainLyrics);
		if (!flag11)
		{
			list3.Add("No usable lyrics");
		}
		bool flag12 = LooksLikeRomanizedLyricsForJapaneseTitle(track.Title, candidate.SyncedLyrics, candidate.PlainLyrics);
		if (flag12)
		{
			list2.Add("Lyrics script");
			list3.Add("Lyrics script does not match");
		}
		int num3 = 0;
		if (num)
		{
			num3 += (flag ? 45 : 50);
		}
		if (flag2)
		{
			num3 += 35;
		}
		else if (flag3)
		{
			num3 += 25;
		}
		else if (flag4)
		{
			num3 += 25;
		}
		else if (flag5)
		{
			num3 += 15;
		}
		if (flag6)
		{
			num3 += 5;
		}
		else if (flag7)
		{
			num3 += 2;
		}
		if (!string.IsNullOrWhiteSpace(candidate.SyncedLyrics))
		{
			num3 += 20;
		}
		if (num2.HasValue)
		{
			if (num2.Value < 0.5)
			{
				num3 += 15;
			}
			else if (num2.Value <= 1.0)
			{
				num3 += 10;
			}
			else if (num2.Value <= 2.0)
			{
				num3 += 5;
			}
		}
		if (flag8)
		{
			num3 -= 40;
		}
		if (flag10)
		{
			num3 -= 60;
		}
		if (flag12)
		{
			num3 -= 30;
		}
		bool flag13 = !string.IsNullOrWhiteSpace(candidate.SyncedLyrics);
		bool flag14 = num && (flag2 || flag3 || flag4 || (flag5 && flag13)) && num2 <= 2.0 && !flag8 && !flag10 && !flag12 && flag11;
		string qualityKey = (flag14 ? "High match" : "Needs review");
		if (flag10 || candidate.Instrumental)
		{
			qualityKey = "Instrumental";
		}
		else if (num2 > 2.0)
		{
			qualityKey = "Duration mismatch";
		}
		else if (!flag2 && !flag3 && !flag4 && !flag5)
		{
			qualityKey = "Artist mismatch";
		}
		else if (flag12)
		{
			qualityKey = "Romanized lyrics";
		}
		else if (string.IsNullOrWhiteSpace(candidate.SyncedLyrics) && !string.IsNullOrWhiteSpace(candidate.PlainLyrics))
		{
			qualityKey = "Plain only";
		}
		return new LyricsCandidate
		{
			Record = candidate,
			Score = num3,
			DurationDifferenceSeconds = num2,
			AutoEligible = flag14,
			ArtistMatchIsCrossScript = flag5,
			LyricsScriptMismatch = flag12,
			MatchedFields = list,
			MismatchedFields = list2,
			RejectionReasons = list3.Distinct<string>(StringComparer.Ordinal).ToArray(),
			QualityKey = qualityKey
		};
	}

	public static LyricsCandidate? SelectSafeAutomaticCandidate(TrackInfo track, IEnumerable<LrclibRecord> records, int minimumScoreGap = 12)
	{
		List<LyricsCandidate> list = (from record in records
			group record by record.Id into @group
			select Evaluate(track, @group.First()) into candidate
			where candidate.AutoEligible
			select candidate).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		List<LyricsCandidate> list2 = (list.Any(HasSyncedLyrics) ? (from candidate in list.Where(HasSyncedLyrics)
			orderby candidate.ArtistMatchIsCrossScript ? 1 : 0, candidate.Score descending, candidate.Record.Id
			select candidate).ToList() : (from candidate in list
			orderby candidate.ArtistMatchIsCrossScript ? 1 : 0, candidate.Score descending, candidate.Record.Id
			select candidate).ToList());
		if (list2[0].ArtistMatchIsCrossScript && (from candidate in list2
			where candidate.ArtistMatchIsCrossScript
			select NormalizeForComparison(candidate.Record.ArtistName) into artist
			where artist.Length > 0
			select artist).Distinct<string>(StringComparer.Ordinal).Count() > 1)
		{
			return null;
		}
		if (!HasSyncedLyrics(list2[0]) && list2.Count > 1 && list2[0].Score - list2[1].Score < minimumScoreGap)
		{
			return null;
		}
		return list2[0];
	}

	public static IReadOnlyList<LyricsCandidate> RankCandidates(TrackInfo track, IEnumerable<LrclibRecord> records)
	{
		return (from record in records
			group record by record.Id into @group
			select Evaluate(track, @group.First()) into candidate
			orderby CandidatePriority(candidate), DurationPriority(candidate), candidate.Score descending, candidate.Record.Id
			select candidate).ToArray();
	}

	private static double DurationPriority(LyricsCandidate candidate)
	{
		return candidate.DurationDifferenceSeconds.HasValue ? Math.Abs(candidate.DurationDifferenceSeconds.Value) : double.MaxValue;
	}

	private static bool HasSyncedLyrics(LyricsCandidate candidate)
	{
		return !string.IsNullOrWhiteSpace(candidate.Record.SyncedLyrics);
	}

	private static int CandidatePriority(LyricsCandidate candidate)
	{
		if (candidate.LyricsScriptMismatch)
		{
			return 4;
		}
		bool flag = HasSyncedLyrics(candidate);
		if (candidate.AutoEligible && flag)
		{
			return 0;
		}
		if (flag)
		{
			return 1;
		}
		if (candidate.AutoEligible)
		{
			return 2;
		}
		return 3;
	}

	internal static bool LooksLikeRomanizedLyricsForJapaneseTitle(string? title, string? syncedLyrics, string? plainLyrics)
	{
		if (!ContainsJapanese(title))
		{
			return false;
		}
		string text = ((!string.IsNullOrWhiteSpace(syncedLyrics)) ? syncedLyrics : (plainLyrics ?? string.Empty));
		if (text.Length == 0)
		{
			return false;
		}
		string text2 = LrcTagPattern.Replace(text, " ").Normalize(NormalizationForm.FormKC).ToLowerInvariant();
		int num = text2.Count(IsJapaneseCharacter);
		int num2 = text2.Count(IsLatinLetter);
		int num3 = num + num2;
		if (num2 < 30 || num3 == 0 || num >= 5 || num * 10 >= num3)
		{
			return false;
		}
		string text3 = RemainingPunctuationPattern.Replace(text2, " ");
		string[] array = (from token in WhitespacePattern.Split(text3.Trim())
			where token.Length > 0
			select token).ToArray();
		if (array.Length < 6)
		{
			return false;
		}
		string[] array2 = array.Where(RomanizedJapaneseHints.Contains).ToArray();
		int num4 = array2.Distinct<string>(StringComparer.Ordinal).Count();
		if (array2.Length >= 5)
		{
			return num4 >= 4;
		}
		return false;
	}

	private static bool ContainsJapanese(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Any(IsJapaneseCharacter);
		}
		return false;
	}

	private static bool IsJapaneseCharacter(char character)
	{
		if ((character < '\u3040' || character > 'ヿ') && (character < '㐀' || character > '䶿') && (character < '一' || character > '鿿') && (character < '豈' || character > '\ufaff'))
		{
			if (character >= '･')
			{
				return character <= 'ﾟ';
			}
			return false;
		}
		return true;
	}

	public static IReadOnlyList<string> ExtractTitleAliases(string? title)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string> { title.Trim() };
		foreach (Match item in BracketAliasPattern.Matches(title))
		{
			AddUnique(list, item.Groups[1].Value);
		}
		AddUnique(list, BracketAliasPattern.Replace(title, " "));
		return list;
	}

	public static string NormalizeForComparison(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string input = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
		input = Regex.Replace(input, "\\b(?:feat(?:uring)?|ft|starring)\\.?\\b", " & ", RegexOptions.IgnoreCase);
		input = ComparisonPunctuationPattern.Replace(input, " ");
		input = BracketAliasPattern.Replace(input, (Match match) => " " + match.Groups[1].Value + " ");
		input = WhitespacePattern.Replace(input.Trim(), " ");
		return RemainingPunctuationPattern.Replace(input, string.Empty);
	}

	public static IReadOnlyList<string> GetKnownArtistSearchAliases(string? artist)
	{
		string text = NormalizeForComparison(artist);
		if (text.Contains("藤井風", StringComparison.Ordinal) || text.Contains("fujiikaze", StringComparison.Ordinal))
		{
			return new string[1] { "Fujii Kaze" };
		}
		return Array.Empty<string>();
	}

	private static HashSet<string> ArtistTokens(string? artist)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		string[] array = ArtistSeparatorPattern.Split((artist ?? string.Empty).Normalize(NormalizationForm.FormKC).ToLowerInvariant());
		for (int i = 0; i < array.Length; i++)
		{
			string text = CanonicalArtistToken(NormalizeForComparison(array[i]));
			if (text.Length > 0)
			{
				hashSet.Add(text);
			}
		}
		return hashSet;
	}

	private static string CanonicalArtistToken(string token)
	{
		if ((!(token == "藤井風") && !(token == "fujiikaze")) || 1 == 0)
		{
			return token;
		}
		return "fujiikaze";
	}

	private static bool IsCrossScriptArtistPair(string? expected, string? actual)
	{
		if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
		{
			return false;
		}
		bool flag = expected.Any(IsLatinLetter);
		bool flag2 = expected.Any((char character) => char.IsLetter(character) && !IsLatinLetter(character));
		bool flag3 = actual.Any(IsLatinLetter);
		bool flag4 = actual.Any((char character) => char.IsLetter(character) && !IsLatinLetter(character));
		if (!(flag2 && flag3) || flag4)
		{
			if (flag4 && flag)
			{
				return !flag2;
			}
			return false;
		}
		return true;
	}

	private static bool IsLatinLetter(char character)
	{
		if (!char.IsLetter(character))
		{
			return false;
		}
		UnicodeCategory unicodeCategory = char.GetUnicodeCategory(character);
		if ((character < 'A' || character > 'ɏ') && (character < 'Ḁ' || character > 'ỿ'))
		{
			if (unicodeCategory == UnicodeCategory.ModifierLetter)
			{
				return character < '\u02ff';
			}
			return false;
		}
		return true;
	}

	private static HashSet<string> EditionTokens(string value)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (Match item in EditionPattern.Matches(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant()))
		{
			string value2 = item.Value;
			value2 = ((!value2.Contains("instrument", StringComparison.OrdinalIgnoreCase) && !value2.Contains("インスト", StringComparison.Ordinal)) ? ((!value2.Contains("live", StringComparison.OrdinalIgnoreCase) && !value2.Contains("ライブ", StringComparison.Ordinal)) ? ((!value2.Contains("remix", StringComparison.OrdinalIgnoreCase) && !value2.Contains("リミックス", StringComparison.Ordinal)) ? ((!value2.Contains("cover", StringComparison.OrdinalIgnoreCase) && !value2.Contains("カバー", StringComparison.Ordinal)) ? ((!value2.Contains("karaoke", StringComparison.OrdinalIgnoreCase) && !value2.Contains("カラオケ", StringComparison.Ordinal)) ? ((!value2.Contains("tv", StringComparison.OrdinalIgnoreCase) && !value2.Contains("テレビ", StringComparison.Ordinal)) ? NormalizeForComparison(value2) : "tvsize") : "karaoke") : "cover") : "remix") : "live") : "instrumental");
			hashSet.Add(value2);
		}
		return hashSet;
	}

	private static void AddUnique(ICollection<string> values, string? value)
	{
		string text = WhitespacePattern.Replace(value?.Trim() ?? string.Empty, " ");
		if (text.Length > 0 && !values.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
		{
			values.Add(text);
		}
	}
}
