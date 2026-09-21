using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FlowLyrics.Core;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public static class LyricsMatcher
{
	private static readonly Regex WhitespacePattern = new Regex("\\s+", RegexOptions.Compiled);

	private static readonly Regex BracketAliasPattern = new Regex("[\\(\\[【「（［]([^\\)\\]】」）］]+)[\\)\\]】」）］]", RegexOptions.Compiled);

	private static readonly Regex ComparisonPunctuationPattern = new Regex("[\\-‐‑‒–—―ーｰ・･,，、/／&＆×=+＋:：;；_]+", RegexOptions.Compiled);

	private static readonly Regex RemainingPunctuationPattern = new Regex("[^\\p{L}\\p{N}]+", RegexOptions.Compiled);

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
		LyricsCandidate best = EvaluateSingle(track, candidate);
		if (track.SearchAlternates == null) return best;

		foreach (SearchMetadataCandidate alternate in track.SearchAlternates)
		{
			if (alternate.IsEmpty || !alternate.CanEstablishIdentity || string.IsNullOrWhiteSpace(alternate.Artist)) continue;
			TrackInfo alternateTrack = track with { Title = alternate.Title, Artist = alternate.Artist, Album = alternate.Album, SearchAlternates = null };
			LyricsCandidate evaluated = EvaluateSingle(alternateTrack, candidate);
			if (IsBetterIdentityMatch(evaluated, best)) best = evaluated;
		}
		return best;
	}

	private static bool IsBetterIdentityMatch(LyricsCandidate candidate, LyricsCandidate current)
	{
		if (candidate.AutoEligible != current.AutoEligible) return candidate.AutoEligible;
		if (candidate.Score != current.Score) return candidate.Score > current.Score;
		if (candidate.ArtistMatchIsCrossScript != current.ArtistMatchIsCrossScript) return !candidate.ArtistMatchIsCrossScript;
		return false;
	}

	private static LyricsCandidate EvaluateSingle(TrackInfo track, LrclibRecord candidate)
	{
		List<string> matched = new();
		List<string> mismatched = new();
		List<string> reasons = new();
		HashSet<string> titles = MetadataNormalizer.TitleAliases(track.Title)
			.Select(NormalizeForComparison).ToHashSet(StringComparer.Ordinal);
		bool titleMatch = titles.Count > 0 && MetadataNormalizer.TitleAliases(candidate.TrackName)
			.Any(alias => titles.Contains(NormalizeForComparison(alias)));
		bool exactTitle = NormalizeForComparison(track.Title).Length > 0
			&& NormalizeForComparison(track.Title) == NormalizeForComparison(candidate.TrackName);
		bool artistMatch = ArtistIdentity.Parse(track.Artist).StronglyMatches(ArtistIdentity.Parse(candidate.ArtistName));
		bool wholeArtist = NormalizeForComparison(track.Artist).Length > 0
			&& NormalizeForComparison(track.Artist) == NormalizeForComparison(candidate.ArtistName);
		bool crossScriptHint = !artistMatch && IsCrossScriptArtistPair(track.Artist, candidate.ArtistName);
		void Check(bool valid, string field, string reason)
		{
			if (valid) matched.Add(field);
			else { mismatched.Add(field); reasons.Add(reason); }
		}
		Check(titleMatch, exactTitle ? "Title" : "Title alias", "Title does not match");
		Check(artistMatch, "Artist", "Artist identity is not established");
		string album = NormalizeForComparison(track.Album);
		bool albumMatch = album.Length > 0 && album == NormalizeForComparison(candidate.AlbumName);
		if (albumMatch) matched.Add("Album");
		else if (album.Length > 0 && !string.IsNullOrWhiteSpace(candidate.AlbumName)) mismatched.Add("Album");

		HashSet<string> editions = RecordingEdition.Signature(track.Title, track.Album);
		// Provider interpretations may remove credits, but must never erase an edition.
		editions.UnionWith(RecordingEdition.Signature(track.OriginalMediaTitle));
		HashSet<string> candidateEditions = RecordingEdition.Signature(candidate.TrackName, candidate.AlbumName);
		bool editionMatch = editions.SetEquals(candidateEditions);
		bool instrumental = editions.Contains("instrumental") || editions.Contains("karaoke");
		bool instrumentalMatch = candidate.Instrumental == instrumental;
		Check(editionMatch, "Version", "Version does not match");
		Check(instrumentalMatch, "Instrumental", "Instrumental status does not match");

		double? difference = track.Duration > TimeSpan.Zero && double.IsFinite(candidate.Duration) && candidate.Duration > 0
			? Math.Abs(track.Duration.TotalSeconds - candidate.Duration) : null;
		bool standardDuration = difference.HasValue && difference.Value <= 2.0;
		// Only a longer video, with independently established identities, may include
		// intro/outro material. Unknown sources retain the audio rule.
		bool videoDuration = !standardDuration && difference.HasValue
			&& MediaSourceClassifier.IsBrowser(track.SourceAppUserModelId)
			&& track.Duration.TotalSeconds > candidate.Duration
			&& difference.Value <= Math.Min(90.0, candidate.Duration * 0.30)
			&& titleMatch && artistMatch && editionMatch && instrumentalMatch;
		Check(standardDuration || videoDuration, "Duration",
			difference.HasValue ? "Duration exceeds source tolerance" : "Duration is unavailable");
		if (videoDuration) matched.Add("Video timeline difference");

		bool usable = candidate.Instrumental || !string.IsNullOrWhiteSpace(candidate.SyncedLyrics)
			|| !string.IsNullOrWhiteSpace(candidate.PlainLyrics);
		if (!usable) reasons.Add("No usable lyrics");
		bool scriptMismatch = LooksLikeRomanizedLyricsForJapaneseTitle(
			track.OriginalMediaTitle ?? track.Title, candidate.SyncedLyrics, candidate.PlainLyrics);
		if (scriptMismatch) { mismatched.Add("Lyrics script"); reasons.Add("Lyrics script does not match"); }

		int score = (titleMatch ? (exactTitle ? 50 : 45) : 0)
			+ (artistMatch ? (wholeArtist ? 35 : 30) : 0)
			+ (albumMatch ? 5 : 0)
			+ (!string.IsNullOrWhiteSpace(candidate.SyncedLyrics) ? 20 : 0)
			+ (standardDuration ? (difference <= 0.5 ? 15 : 5) : 0)
			- (editionMatch ? 0 : 40) - (instrumentalMatch ? 0 : 60) - (scriptMismatch ? 30 : 0);
		bool eligible = titleMatch && artistMatch && (standardDuration || videoDuration)
			&& editionMatch && instrumentalMatch && usable && !scriptMismatch;
		return new LyricsCandidate
		{
			Record = candidate,
			Score = score,
			DurationDifferenceSeconds = difference,
			AutoEligible = eligible,
			ArtistMatchIsCrossScript = crossScriptHint,
			LyricsScriptMismatch = scriptMismatch,
			UsesVideoDurationTolerance = videoDuration,
			MatchedFields = matched,
			MismatchedFields = mismatched,
			RejectionReasons = reasons,
			QualityKey = eligible ? (candidate.Instrumental ? "Instrumental" : "High match")
				: !titleMatch ? "Needs review" : !artistMatch ? "Artist mismatch"
				: !(standardDuration || videoDuration) ? "Duration mismatch" : "Needs review"
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

	public static LyricsCandidate? SelectBestEffortCandidate(IEnumerable<LyricsCandidate> candidates)
	{
		return candidates
			.GroupBy(candidate => candidate.Record.Id)
			.Select(group => group.First())
			.Where(candidate => candidate.AutoEligible && (candidate.Record.Instrumental
				|| !string.IsNullOrWhiteSpace(candidate.Record.SyncedLyrics)
				|| !string.IsNullOrWhiteSpace(candidate.Record.PlainLyrics)))
			.OrderByDescending(candidate => candidate.Score)
			.ThenByDescending(HasSyncedLyrics)
			.ThenBy(candidate => candidate.LyricsScriptMismatch ? 1 : 0)
			.ThenBy(DurationPriority)
			.ThenBy(candidate => candidate.Record.Id)
			.FirstOrDefault();
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

	public static IReadOnlyList<string> ExtractTitleAliases(string? title) => MetadataNormalizer.TitleAliases(title);

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

	public static IReadOnlyList<string> GetKnownArtistSearchAliases(string? artist) => ArtistIdentity.Parse(artist).SearchCredits;

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

}
