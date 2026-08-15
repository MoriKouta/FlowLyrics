using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowLyrics.Core;

public sealed record RepairedProviderMetadata(
	string Title,
	string Artist,
	string Album,
	IReadOnlyList<SearchMetadataCandidate>? SearchAlternates = null);

/// <summary>
/// Repairs provider-specific GSMTC metadata before it is displayed, cached, or
/// sent to a lyrics provider. Browser repair also retains a small ordered set of
/// alternate title/artist interpretations for ambiguous YouTube conventions.
/// </summary>
public static partial class ProviderMetadataRepair
{
	private static readonly (char Open, char Close)[] TitleQuotePairs =
	[
		('「', '」'), ('『', '』'), ('“', '”'), ('‘', '’'), ('"', '"'), ('\'', '\'')
	];

	public static RepairedProviderMetadata Repair(
		string? sourceAppUserModelId,
		string? title,
		string? artist,
		string? album,
		string? albumArtist = null)
	{
		string repairedTitle = MetadataNormalizer.NormalizeWhitespace(title);
		string repairedArtist = MetadataNormalizer.NormalizeWhitespace(artist);
		string repairedAlbum = MetadataNormalizer.NormalizeWhitespace(album);
		string repairedAlbumArtist = MetadataNormalizer.NormalizeWhitespace(albumArtist);

		if (string.IsNullOrWhiteSpace(repairedArtist) && !string.IsNullOrWhiteSpace(repairedAlbumArtist))
		{
			repairedArtist = repairedAlbumArtist;
		}

		RepairedProviderMetadata metadata = new(repairedTitle, repairedArtist, repairedAlbum);
		if (MediaSourceClassifier.IsAppleMusic(sourceAppUserModelId))
		{
			metadata = RepairAppleMusic(metadata, repairedAlbumArtist);
		}
		if (MediaSourceClassifier.IsBrowser(sourceAppUserModelId))
		{
			metadata = RepairBrowser(metadata);
		}
		return metadata;
	}

	private static RepairedProviderMetadata RepairAppleMusic(RepairedProviderMetadata metadata, string albumArtist)
	{
		Match combined = AppleCombinedArtistAlbumRegex().Match(metadata.Artist);
		if (!combined.Success) return metadata;

		string parsedArtist = MetadataNormalizer.NormalizeWhitespace(combined.Groups["artist"].Value);
		string parsedAlbum = MetadataNormalizer.NormalizeWhitespace(combined.Groups["album"].Value);
		if (string.IsNullOrWhiteSpace(parsedArtist) || string.IsNullOrWhiteSpace(parsedAlbum)) return metadata;

		// Apple Music can expose the same "artist — album" compound in both
		// Artist and AlbumArtist. Prefer the parsed artist unless AlbumArtist is
		// independently clean and identifies that same artist.
		string cleanAlbumArtist = albumArtist;
		Match albumArtistCombined = AppleCombinedArtistAlbumRegex().Match(cleanAlbumArtist);
		if (albumArtistCombined.Success)
		{
			cleanAlbumArtist = MetadataNormalizer.NormalizeWhitespace(albumArtistCombined.Groups["artist"].Value);
		}
		string repairedArtist = LooksLikeSameArtist(cleanAlbumArtist, parsedArtist)
			? cleanAlbumArtist
			: parsedArtist;

		return metadata with
		{
			Artist = repairedArtist,
			Album = string.IsNullOrWhiteSpace(metadata.Album) ? parsedAlbum : metadata.Album
		};
	}

	private static RepairedProviderMetadata RepairBrowser(RepairedProviderMetadata metadata)
	{
		if (string.IsNullOrWhiteSpace(metadata.Title)) return metadata;

		string originalTitle = metadata.Title;
		bool hasProductionMarker = ProductionMarkerRegex().IsMatch(originalTitle);
		List<SearchMetadataCandidate> alternatives = new();

		if (TryExtractQuotedTitle(originalTitle, out string prefix, out string quotedTitle))
		{
			IReadOnlyList<string> artistAliases = ExpandArtistAliases(CleanCredit(prefix));
			string repairedArtist = SelectPrimaryArtist(artistAliases, metadata.Artist);
			if (!string.IsNullOrWhiteSpace(quotedTitle)
				&& (hasProductionMarker || artistAliases.Count > 0 || IsGenericChannelName(metadata.Artist)))
			{
				AddArtistAlternates(alternatives, quotedTitle, repairedArtist, metadata.Album, artistAliases);
				if (!IsGenericChannelName(metadata.Artist))
				{
					AddAlternative(alternatives, quotedTitle, metadata.Artist, metadata.Album);
				}
				return CreateResult(quotedTitle, repairedArtist, metadata.Album, alternatives);
			}
		}

		string cleanedTitle = StripProductionNoise(originalTitle);
		if (string.IsNullOrWhiteSpace(cleanedTitle)) cleanedTitle = originalTitle;

		Match slash = SlashCreditRegex().Match(cleanedTitle);
		if (slash.Success)
		{
			string left = CleanCredit(slash.Groups["left"].Value);
			string right = CleanCredit(slash.Groups["right"].Value);
			if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
			{
				if (ArtistAppearsInChannel(right, metadata.Artist))
				{
					AddAlternative(alternatives, right, left, metadata.Album);
					return CreateResult(left, right, metadata.Album, alternatives);
				}
				if (ArtistAppearsInChannel(left, metadata.Artist))
				{
					AddAlternative(alternatives, left, right, metadata.Album);
					return CreateResult(right, left, metadata.Album, alternatives);
				}

				// "title / credited performer" is common in Japanese uploads. A
				// non-generic channel often identifies the composer more accurately,
				// so retain the credit as an alternate instead of replacing it.
				if (IsGenericChannelName(metadata.Artist))
				{
					IReadOnlyList<string> aliases = ExpandArtistAliases(right);
					string repairedArtist = SelectPrimaryArtist(aliases, metadata.Artist);
					AddArtistAlternates(alternatives, left, repairedArtist, metadata.Album, aliases);
					AddAlternative(alternatives, right, left, metadata.Album);
					return CreateResult(left, repairedArtist, metadata.Album, alternatives);
				}

				AddAlternative(alternatives, left, right, metadata.Album);
				AddAlternative(alternatives, right, left, metadata.Album);
				return CreateResult(left, metadata.Artist, metadata.Album, alternatives);
			}
		}

		Match dash = DashCreditRegex().Match(cleanedTitle);
		if (dash.Success)
		{
			string left = CleanCredit(dash.Groups["left"].Value);
			string right = CleanCredit(dash.Groups["right"].Value);
			if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
			{
				bool leftIsArtist = ArtistAppearsInChannel(left, metadata.Artist);
				bool rightIsArtist = ArtistAppearsInChannel(right, metadata.Artist);
				if (leftIsArtist || (!rightIsArtist && hasProductionMarker))
				{
					IReadOnlyList<string> aliases = ExpandArtistAliases(left);
					string repairedArtist = SelectPrimaryArtist(aliases, metadata.Artist);
					AddArtistAlternates(alternatives, right, repairedArtist, metadata.Album, aliases);
					AddAlternative(alternatives, left, right, metadata.Album);
					return CreateResult(right, repairedArtist, metadata.Album, alternatives);
				}
				if (rightIsArtist)
				{
					AddAlternative(alternatives, right, left, metadata.Album);
					return CreateResult(left, right, metadata.Album, alternatives);
				}
				if (IsGenericChannelName(metadata.Artist))
				{
					IReadOnlyList<string> aliases = ExpandArtistAliases(left);
					string repairedArtist = SelectPrimaryArtist(aliases, metadata.Artist);
					AddArtistAlternates(alternatives, right, repairedArtist, metadata.Album, aliases);
					AddAlternative(alternatives, left, right, metadata.Album);
					return CreateResult(right, repairedArtist, metadata.Album, alternatives);
				}
				AddAlternative(alternatives, right, left, metadata.Album);
				AddAlternative(alternatives, left, right, metadata.Album);
			}
		}

		return CreateResult(cleanedTitle, metadata.Artist, metadata.Album, alternatives);
	}

	private static bool TryExtractQuotedTitle(string value, out string prefix, out string title)
	{
		prefix = string.Empty;
		title = string.Empty;
		int bestStart = int.MaxValue;
		int bestEnd = -1;
		foreach ((char open, char close) in TitleQuotePairs)
		{
			int start = value.IndexOf(open);
			if (start < 0 || start >= bestStart) continue;
			int end = open == close
				? value.IndexOf(close, start + 1)
				: value.LastIndexOf(close);
			if (end <= start + 1) continue;
			bestStart = start;
			bestEnd = end;
		}
		if (bestEnd <= bestStart) return false;

		prefix = value[..bestStart];
		title = TrimTitlePunctuation(value[(bestStart + 1)..bestEnd]);
		return title.Length > 0;
	}

	private static IReadOnlyList<string> ExpandArtistAliases(string value)
	{
		string credit = CleanCredit(value);
		if (string.IsNullOrWhiteSpace(credit)) return Array.Empty<string>();

		List<string> aliases = new();
		MatchCollection bracketMatches = ArtistAliasBracketRegex().Matches(credit);
		if (bracketMatches.Count > 0)
		{
			AddUnique(aliases, ArtistAliasBracketRegex().Replace(credit, " "));
			foreach (Match match in bracketMatches)
			{
				AddUnique(aliases, match.Groups["alias"].Value);
			}
		}
		else
		{
			IReadOnlyList<string> scriptParts = SplitOnScriptChanges(credit);
			if (scriptParts.Count > 1)
			{
				foreach (string part in scriptParts) AddUnique(aliases, part);
			}
			else
			{
				AddUnique(aliases, credit);
			}
		}
		return aliases;
	}

	private static IReadOnlyList<string> SplitOnScriptChanges(string value)
	{
		List<string> parts = new();
		StringBuilder current = new();
		int currentScript = 0;
		foreach (char character in value)
		{
			int script = ScriptOf(character);
			if (script == 0)
			{
				if (current.Length > 0 && char.IsWhiteSpace(character)) current.Append(' ');
				continue;
			}
			if (currentScript != 0 && script != currentScript)
			{
				AddUnique(parts, current.ToString());
				current.Clear();
			}
			currentScript = script;
			current.Append(character);
		}
		AddUnique(parts, current.ToString());
		return parts.Where(part => part.Any(char.IsLetterOrDigit)).ToArray();
	}

	private static int ScriptOf(char character)
	{
		if (character is >= '\uac00' and <= '\ud7af' or >= '\u1100' and <= '\u11ff') return 2;
		if (character is >= '\u3040' and <= '\u30ff' or >= '\u3400' and <= '\u9fff') return 3;
		if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '\u00c0' and <= '\u024f' || char.IsDigit(character)) return 1;
		return char.IsLetter(character) ? 4 : 0;
	}

	private static string SelectPrimaryArtist(IReadOnlyList<string> aliases, string channelArtist)
	{
		foreach (string alias in aliases)
		{
			if (ArtistAppearsInChannel(alias, channelArtist)) return alias;
		}
		if (aliases.Count > 0) return aliases[0];
		return channelArtist;
	}

	private static void AddArtistAlternates(
		List<SearchMetadataCandidate> alternatives,
		string title,
		string primaryArtist,
		string album,
		IReadOnlyList<string> aliases)
	{
		foreach (string alias in aliases)
		{
			if (!LooksLikeSameArtist(alias, primaryArtist)) AddAlternative(alternatives, title, alias, album);
		}
	}

	private static RepairedProviderMetadata CreateResult(
		string title,
		string artist,
		string album,
		List<SearchMetadataCandidate> alternatives)
	{
		title = TrimTitlePunctuation(title);
		artist = CleanCredit(artist);
		string primaryKey = CandidateKey(title, artist, album);
		SearchMetadataCandidate[] distinctAlternatives = alternatives
			.Where(candidate => !string.Equals(CandidateKey(candidate.Title, candidate.Artist, candidate.Album), primaryKey, StringComparison.Ordinal))
			.GroupBy(candidate => CandidateKey(candidate.Title, candidate.Artist, candidate.Album), StringComparer.Ordinal)
			.Select(group => group.First())
			.Take(8)
			.ToArray();
		return new RepairedProviderMetadata(title, artist, album, distinctAlternatives);
	}

	private static void AddAlternative(List<SearchMetadataCandidate> alternatives, string title, string artist, string album)
	{
		title = TrimTitlePunctuation(title);
		artist = CleanCredit(artist);
		if (title.Length == 0 || artist.Length == 0) return;
		alternatives.Add(new SearchMetadataCandidate(title, artist, album));
	}

	private static void AddUnique(List<string> values, string value)
	{
		value = CleanCredit(value);
		if (value.Length == 0) return;
		if (!values.Any(existing => LooksLikeSameArtist(existing, value))) values.Add(value);
	}

	private static string CandidateKey(string title, string artist, string album) =>
		MetadataNormalizer.NormalizeWhitespace(title).ToLowerInvariant() + "|"
		+ MetadataNormalizer.NormalizeWhitespace(artist).ToLowerInvariant() + "|"
		+ MetadataNormalizer.NormalizeWhitespace(album).ToLowerInvariant();

	private static string StripProductionNoise(string value)
	{
		string cleaned = ProductionPrefixRegex().Replace(value, string.Empty);
		for (int i = 0; i < 3; i++)
		{
			string next = ProductionSuffixRegex().Replace(cleaned, string.Empty);
			if (string.Equals(cleaned, next, StringComparison.Ordinal)) break;
			cleaned = next;
		}
		return TrimTitlePunctuation(cleaned);
	}

	private static string CleanCredit(string value)
	{
		string cleaned = ProductionPrefixRegex().Replace(value, string.Empty);
		cleaned = ProductionTokenBracketRegex().Replace(cleaned, string.Empty);
		cleaned = FeaturingCreditRegex().Replace(cleaned, string.Empty);
		return TrimTitlePunctuation(cleaned);
	}

	private static bool ArtistAppearsInChannel(string artist, string channel)
	{
		string left = CanonicalizeArtist(artist);
		string right = CanonicalizeArtist(channel);
		return left.Length >= 2 && right.Length >= 2
			&& (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
				|| right.Contains(left, StringComparison.OrdinalIgnoreCase)
				|| left.Contains(right, StringComparison.OrdinalIgnoreCase));
	}

	private static bool LooksLikeSameArtist(string first, string second)
	{
		string left = CanonicalizeArtist(first);
		string right = CanonicalizeArtist(second);
		return left.Length >= 2 && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsGenericChannelName(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return true;
		string normalized = value.Trim();
		return ChannelSuffixRegex().IsMatch(normalized)
			|| LabelChannelRegex().IsMatch(normalized)
			|| string.Equals(normalized, "YouTube", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(normalized, "YouTube Music", StringComparison.OrdinalIgnoreCase);
	}

	private static string CanonicalizeArtist(string value)
	{
		string normalized = MetadataNormalizer.NormalizeWhitespace(value);
		normalized = ChannelSuffixRegex().Replace(normalized, string.Empty);
		normalized = normalized.Normalize(NormalizationForm.FormKC);
		return ArtistPunctuationRegex().Replace(normalized, string.Empty).ToLowerInvariant();
	}

	private static string TrimTitlePunctuation(string value) =>
		MetadataNormalizer.NormalizeWhitespace(value).Trim(' ', '-', '–', '—', '|', '/', '／', ':', '：');

	[GeneratedRegex(@"^(?<artist>.+?)\s+[—–]\s+(?<album>.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex AppleCombinedArtistAlbumRegex();

	[GeneratedRegex(@"^(?<left>.+?)\s+[/／]\s+(?<right>.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex SlashCreditRegex();

	[GeneratedRegex(@"^(?<left>.+?)\s+(?:-|–|—)\s+(?<right>.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex DashCreditRegex();

	[GeneratedRegex(@"\b(?:offi?cial|music\s+video|lyric\s+video|lyrics?|m\s*/\s*v|mv|pv)\b|【\s*mv\s*】|\[\s*mv\s*\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ProductionMarkerRegex();

	[GeneratedRegex(@"^\s*(?:(?:[\(\[（【]\s*)?(?:(?:offi?cial\s+)?(?:music\s+)?video|offi?cial\s+(?:audio|mv)|m\s*/\s*v|mv|pv)(?:\s*[\)\]）】])?\s*[:：|\-–—]*\s*)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ProductionPrefixRegex();

	[GeneratedRegex(@"\s*(?:[-|]\s*)?(?:[\(\[（【]\s*)?(?:(?:offi?cial)(?:\s+(?:music\s+)?video|\s+audio|\s+mv)?|(?:music|lyric)\s+video|lyrics?|m\s*/\s*v|mv|pv)\s*(?:[\)\]）】])?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ProductionSuffixRegex();

	[GeneratedRegex(@"[\(\[（【]\s*(?:(?:offi?cial)(?:\s+(?:music\s+)?video|\s+audio|\s+mv)?|(?:music|lyric)\s+video|lyrics?|m\s*/\s*v|mv|pv)\s*[\)\]）】]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ProductionTokenBracketRegex();

	[GeneratedRegex(@"\s+(?:feat(?:uring)?\.?|ft\.?)\s*.*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex FeaturingCreditRegex();

	[GeneratedRegex(@"[\(（](?<alias>[^\)）]{1,80})[\)）]", RegexOptions.CultureInvariant)]
	private static partial Regex ArtistAliasBracketRegex();

	[GeneratedRegex(@"\s*(?:[-–—]\s*)?(?:(?:offi?cial\s+)?(?:channel|topic)|vevo)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ChannelSuffixRegex();

	[GeneratedRegex(@"(?:^|\s)(?:hybe\s+labels?|smtown|kq\s+entertainment|[\w.-]+\s+(?:entertainment|labels?|records?))(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex LabelChannelRegex();

	[GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)]
	private static partial Regex ArtistPunctuationRegex();
}
