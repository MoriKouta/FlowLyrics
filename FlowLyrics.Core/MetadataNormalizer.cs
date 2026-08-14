using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowLyrics.Core;

public sealed record SearchMetadataCandidate(string Title, string Artist, string Album)
{
	public bool IsEmpty => string.IsNullOrWhiteSpace(Title);
}

public static partial class MetadataNormalizer
{
	public static SearchMetadataCandidate NormalizeForSearch(string? title, string? artist, string? album)
	{
		string normalizedTitle = NormalizeWhitespace(title);
		normalizedTitle = NoiseBracketRegex().Replace(normalizedTitle, string.Empty);
		normalizedTitle = FeaturingSuffixRegex().Replace(normalizedTitle, string.Empty);
		normalizedTitle = RemasterSuffixRegex().Replace(normalizedTitle, string.Empty);
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
		return result;
	}

	public static string NormalizeWhitespace(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		string normalized = value.Normalize(NormalizationForm.FormKC).Replace('\u3000', ' ');
		return WhitespaceRegex().Replace(normalized, " ").Trim();
	}

	[GeneratedRegex(@"\s*[\(\[\{]\s*(?:official\s+(?:audio|video|mv)|music\s+video|lyric\s+video|lyrics?)\s*[\)\]\}]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex NoiseBracketRegex();

	[GeneratedRegex(@"\s*(?:[\(\[]\s*)?(?:feat\.?|ft\.?|featuring)\s+.+?(?:[\)\]]\s*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex FeaturingSuffixRegex();

	[GeneratedRegex(@"\s*(?:[-–—]\s*)?(?:[\(\[]\s*)?(?:\d{4}\s+)?remaster(?:ed)?(?:\s+\d{4})?\s*(?:[\)\]]\s*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex RemasterSuffixRegex();

	[GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
	private static partial Regex WhitespaceRegex();
}
