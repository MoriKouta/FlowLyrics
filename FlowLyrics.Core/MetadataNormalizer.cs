using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowLyrics.Core;

public sealed record SearchMetadataCandidate(string Title, string Artist, string Album)
{
	public bool IsEmpty => string.IsNullOrWhiteSpace(Title);
	public bool CanEstablishIdentity { get; init; } = true;
	public string Evidence { get; init; } = "Provider metadata";
}

public static partial class MetadataNormalizer
{
	public static SearchMetadataCandidate NormalizeForSearch(string? title, string? artist, string? album)
	{
		string normalizedTitle = NormalizeWhitespace(title);
		normalizedTitle = NoiseBracketRegex().Replace(normalizedTitle, string.Empty);
		normalizedTitle = NormalizeWhitespace(normalizedTitle).Trim(' ', '-', '–', '—');

		return new SearchMetadataCandidate(
			normalizedTitle,
			NormalizeWhitespace(artist),
			NormalizeWhitespace(album));
	}

	public static IReadOnlyList<SearchMetadataCandidate> BuildCandidates(string? title, string? artist, string? album)
	{
		SearchMetadataCandidate raw = new(
			title?.Trim() ?? string.Empty,
			artist?.Trim() ?? string.Empty,
			album?.Trim() ?? string.Empty);
		SearchMetadataCandidate normalized = NormalizeForSearch(title, artist, album);
		List<SearchMetadataCandidate> result = new() { raw };
		if (!string.Equals(raw.Title, normalized.Title, StringComparison.Ordinal)
			|| !string.Equals(raw.Artist, normalized.Artist, StringComparison.Ordinal)
			|| !string.Equals(raw.Album, normalized.Album, StringComparison.Ordinal))
		{
			result.Add(normalized);
		}
		foreach (string alias in TitleAliases(title))
		{
			if (!result.Any(item => item.Title == alias)) result.Add(new(alias, raw.Artist, raw.Album));
		}
		ArtistIdentity identity = ArtistIdentity.Parse(artist);
		foreach (string credit in identity.SearchCredits)
		{
			foreach (string alias in TitleAliases(title))
				if (!result.Any(item => item.Title == alias && item.Artist == credit))
					result.Add(new(alias, credit, raw.Album) { Evidence = "Explicit artist credit" });
		}
		return result;
	}

	public static string ComparisonKey(string? value) => Regex.Replace(
		NormalizeWhitespace(value).ToLowerInvariant(), @"[^\p{L}\p{N}]", string.Empty);

	public static IReadOnlyList<string> TitleAliases(string? title)
	{
		string raw = NormalizeWhitespace(title);
		List<string> aliases = new();
		void Add(string value) { if (value.Length > 0 && !aliases.Contains(value, StringComparer.OrdinalIgnoreCase)) aliases.Add(value); }
		Add(raw);
		string clean = NoiseBracketRegex().Replace(raw, string.Empty).Trim();
		Add(clean);
		if (TryReleaseTitle(clean, out string releaseTitle, out _)) { clean = releaseTitle; Add(clean); }
		if (TryBilingualTitle(clean, out string primary, out string alternate)) { Add(primary); Add(alternate); }
		return aliases;
	}

	public static bool TryBilingualTitle(string value, out string primary, out string alternate)
	{
		primary = NormalizeWhitespace(value);
		alternate = string.Empty;
		Match match = Regex.Match(primary, @"^(?<main>.+?)\s*\((?<alias>[^()]+)\)$");
		if (!match.Success) return false;
		string body = match.Groups["main"].Value.Trim();
		string alias = match.Groups["alias"].Value.Trim();
		if (RecordingEdition.HasMarker(alias) || RecordingEdition.HasMarker(body)) return false;
		bool IsLatin(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '\u00c0' and <= '\u024f';
		bool LatinOnly(string s) => s.Any(char.IsLetter) && s.Where(char.IsLetter).All(IsLatin);
		bool NonLatin(string s) => s.Any(c => char.IsLetter(c) && !IsLatin(c));
		if (!((NonLatin(body) && LatinOnly(alias)) || (LatinOnly(body) && NonLatin(alias)))) return false;
		primary = body;
		alternate = alias;
		return true;
	}

	public static bool TryReleaseTitle(string value, out string title, out string work)
	{
		// Only explicit work credits, never arbitrary parentheses or dash suffixes.
		Match match = Regex.Match(NormalizeWhitespace(value),
			@"^(?<title>.+?)\s*(?:[-–—]\s*|\()TV\s*アニメ\s*[「『](?<work>[^」』]+)[」』]\)?$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			// A quoted work and an explicit Soundtrack suffix distinguish release
			// credits from real titles such as "Theme From Jurassic Park".
			match = Regex.Match(NormalizeWhitespace(value),
				"^(?<title>.+?)\\s*(?:[-–—]\\s*From\\s+[\"“](?<work>[^\"”]+)[\"”]\\s+Soundtrack|\\(From\\s+[\"“](?<work>[^\"”]+)[\"”]\\s+Soundtrack\\))$",
				RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		}
		title = match.Success ? match.Groups["title"].Value.Trim() : value;
		work = match.Success ? match.Groups["work"].Value.Trim() : string.Empty;
		return match.Success && title.Length > 0;
	}

	public static string NormalizeWhitespace(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		string normalized = value.Normalize(NormalizationForm.FormKC).Replace('\u3000', ' ');
		return WhitespaceRegex().Replace(normalized, " ").Trim();
	}

	[GeneratedRegex(@"\s*[\(\[\{]\s*(?:official\s+(?:audio|video|mv)|music\s+video|lyric\s+video|lyrics?)\s*[\)\]\}]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex NoiseBracketRegex();

	[GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
	private static partial Regex WhitespaceRegex();
}
